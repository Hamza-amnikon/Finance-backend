using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Repositories;

namespace RoleService.API.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;

        public UserService(
            IUserRepository userRepository,
            IRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
        }

        public async Task<List<UserResponse>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            var users = await _userRepository.GetAllAsync(
                cancellationToken);

            var roles = await _roleRepository.GetAllAsync(
                cancellationToken);

            var rolesById = roles.ToDictionary(role => role.RoleId);
            var responses = new List<UserResponse>();

            foreach (var user in users)
            {
                if (!rolesById.TryGetValue(user.RoleId, out var role))
                {
                    throw new InvalidOperationException(
                        "An assigned user role was not found.");
                }

                responses.Add(MapToResponse(user, role));
            }

            return responses;
        }

        public async Task<UserResponse?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            ValidateUserId(userId);

            var user = await _userRepository.GetByIdAsync(
                userId,
                cancellationToken);

            if (user == null)
            {
                return null;
            }

            var role = await GetRequiredRoleAsync(
                user.RoleId,
                requireActive: false,
                cancellationToken);

            return MapToResponse(user, role);
        }

        public async Task<(UserChangeResult Result, UserResponse? User)> CreateAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            if (request.TenantId == Guid.Empty ||
                request.ObjectId == Guid.Empty)
            {
                throw new ValidationException(
                    "Provide valid Microsoft tenant and object IDs.");
            }

            var role = await GetRequiredRoleAsync(
                request.RoleId,
                requireActive: true,
                cancellationToken);

            var existingUser = await _userRepository.GetByMicrosoftIdentityAsync(
                request.TenantId,
                request.ObjectId,
                cancellationToken);

            if (existingUser != null)
            {
                return (UserChangeResult.DuplicateIdentity, null);
            }

            var user = new User
            {
                TenantId = request.TenantId,
                ObjectId = request.ObjectId,
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                RoleId = role.RoleId,
                IsActive = request.IsActive
            };

            _userRepository.Add(user);

            try
            {
                await _userRepository.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
                when (IsDuplicateIdentity(exception))
            {
                return (UserChangeResult.DuplicateIdentity, null);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 547 })
            {
                throw new ValidationException(
                    "The selected role changed or was removed. Reload the roles and try again.",
                    exception);
            }

            return (UserChangeResult.Success, MapToResponse(user, role));
        }

        public async Task<(UserChangeResult Result, UserResponse? User)> UpdateAsync(
            Guid userId,
            UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            ValidateUserId(userId);
            ValidateRequest(request);

            var user = await _userRepository.GetByIdAsync(
                userId,
                cancellationToken);

            if (user == null)
            {
                return (UserChangeResult.NotFound, null);
            }

            var role = await GetRequiredRoleAsync(
                request.RoleId,
                requireActive: request.IsActive || request.RoleId != user.RoleId,
                cancellationToken);

            _userRepository.SetOriginalRowVersion(
                user,
                request.RowVersion);

            user.Name = request.Name.Trim();
            user.Email = request.Email.Trim();
            user.RoleId = role.RoleId;
            user.IsActive = request.IsActive;
            user.ModifiedDate = DateTime.UtcNow;

            try
            {
                await _userRepository.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (UserChangeResult.ConcurrencyConflict, null);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 547 })
            {
                throw new ValidationException(
                    "The selected role changed or was removed. Reload the roles and try again.",
                    exception);
            }

            return (UserChangeResult.Success, MapToResponse(user, role));
        }

        public async Task<(UserChangeResult Result, UserResponse? User)> UpdateMicrosoftIdentityAsync(
            Guid userId,
            UpdateMicrosoftIdentityRequest request,
            CancellationToken cancellationToken)
        {
            ValidateUserId(userId);
            ValidateRequest(request);

            if (request.ObjectId == Guid.Empty)
            {
                throw new ValidationException(
                    "Provide the confirmed Microsoft user object ID.");
            }

            var user = await _userRepository.GetByIdAsync(
                userId,
                cancellationToken);

            if (user == null)
            {
                return (UserChangeResult.NotFound, null);
            }

            if (!user.RowVersion.SequenceEqual(request.RowVersion))
            {
                return (UserChangeResult.ConcurrencyConflict, null);
            }

            var existingUser = await _userRepository.GetByMicrosoftIdentityAsync(
                user.TenantId,
                request.ObjectId,
                cancellationToken);

            if (existingUser != null && existingUser.UserId != user.UserId)
            {
                return (UserChangeResult.DuplicateIdentity, null);
            }

            var role = await GetRequiredRoleAsync(
                user.RoleId,
                requireActive: user.IsActive,
                cancellationToken);

            _userRepository.SetOriginalRowVersion(
                user,
                request.RowVersion);

            user.ObjectId = request.ObjectId;
            user.ModifiedDate = DateTime.UtcNow;

            try
            {
                await _userRepository.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (UserChangeResult.ConcurrencyConflict, null);
            }
            catch (DbUpdateException exception)
                when (IsDuplicateIdentity(exception))
            {
                return (UserChangeResult.DuplicateIdentity, null);
            }

            return (UserChangeResult.Success, MapToResponse(user, role));
        }

        private async Task<Roles> GetRequiredRoleAsync(
            int roleId,
            bool requireActive,
            CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                throw new ValidationException(
                    "Select a valid role.");
            }

            var role = await _roleRepository.GetByIdAsync(
                roleId,
                cancellationToken);

            if (role == null)
            {
                throw new ValidationException(
                    "The selected role was not found.");
            }

            if (requireActive && role.Status != RoleStatus.Active)
            {
                throw new ValidationException(
                    "Select an active role before assigning or enabling a user.");
            }

            return role;
        }

        private static void ValidateUserId(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ValidationException(
                    "Select a valid user.");
            }
        }

        private static void ValidateRequest(object? request)
        {
            if (request == null)
            {
                throw new ValidationException(
                    "User details are required.");
            }

            Validator.ValidateObject(
                request,
                new ValidationContext(request),
                validateAllProperties: true);
        }

        private static bool IsDuplicateIdentity(DbUpdateException exception)
        {
            return exception.InnerException is SqlException { Number: 2601 or 2627 };
        }

        private static UserResponse MapToResponse(
            User user,
            Roles role)
        {
            return new UserResponse
            {
                UserId = user.UserId,
                TenantId = user.TenantId,
                ObjectId = user.ObjectId,
                Name = user.Name,
                Email = user.Email,
                RoleId = user.RoleId,
                Role = role.Role,
                IsActive = user.IsActive,
                CreatedDate = user.CreatedDate,
                ModifiedDate = user.ModifiedDate,
                RowVersion = user.RowVersion.ToArray()
            };
        }
    }
}
