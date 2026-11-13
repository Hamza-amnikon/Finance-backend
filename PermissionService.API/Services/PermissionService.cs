using System.ComponentModel.DataAnnotations;
using PermissionService.API.DTOs;
using PermissionService.API.Models;
using PermissionService.API.Repositories;

namespace PermissionService.API.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IPermissionRepository _repository;

        public PermissionService(
            IPermissionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<PermissionResponse>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            var permissions = await _repository.GetAllAsync(
                cancellationToken);

            var responses = new List<PermissionResponse>();

            foreach (var permission in permissions)
            {
                responses.Add(
                    MapToResponse(permission));
            }

            return responses;
        }

        public async Task<PermissionResponse?> GetByIdAsync(
            int permissionId,
            CancellationToken cancellationToken)
        {
            ValidatePermissionId(permissionId);

            var permission = await _repository.GetByIdAsync(
                permissionId,
                cancellationToken);

            if (permission == null)
            {
                return null;
            }

            return MapToResponse(permission);
        }

        public async Task<PermissionResponse> CreateAsync(
            PermissionRequest request,
            CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            var permissionCode = request.PermissionCode.Trim();

            var existingPermission = await _repository.GetByCodeAsync(
                permissionCode,
                cancellationToken);

            if (existingPermission != null)
            {
                throw new InvalidOperationException(
                    "A permission with this code already exists.");
            }

            var permission = new Permission
            {
                PermissionCode = permissionCode,
                Module = request.Module.Trim(),
                PageName = request.PageName.Trim(),
                Route = request.Route?.Trim() ?? string.Empty,
                Status = request.Status,
                CreatedDate = DateTime.UtcNow
            };

            await _repository.AddAsync(
                permission,
                cancellationToken);

            return MapToResponse(permission);
        }

        public async Task<PermissionResponse?> UpdateAsync(
            int permissionId,
            PermissionUpdateRequest request,
            CancellationToken cancellationToken)
        {
            ValidatePermissionId(permissionId);
            ValidateRequest(request);

            var permission = await _repository.GetByIdAsync(
                permissionId,
                cancellationToken);

            if (permission == null)
            {
                return null;
            }

            var permissionCode = request.PermissionCode.Trim();

            var existingPermission = await _repository.GetByCodeAsync(
                permissionCode,
                cancellationToken);

            if (existingPermission != null &&
                existingPermission.PermissionId != permissionId)
            {
                throw new InvalidOperationException(
                    "A permission with this code already exists.");
            }

            permission.PermissionCode = permissionCode;
            permission.Module = request.Module.Trim();
            permission.PageName = request.PageName.Trim();
            permission.Route = request.Route?.Trim() ?? string.Empty;
            permission.Status = request.Status;
            permission.ModifiedDate = DateTime.UtcNow;

            await _repository.UpdateAsync(
                permission,
                request.RowVersion,
                cancellationToken);

            return MapToResponse(permission);
        }

        private static void ValidatePermissionId(
            int permissionId)
        {
            if (permissionId <= 0)
            {
                throw new ValidationException(
                    "Select a valid permission.");
            }
        }

        private static void ValidateRequest(
            PermissionRequest request)
        {
            if (request == null)
            {
                throw new ValidationException(
                    "Permission details are required.");
            }

            var validationContext = new ValidationContext(request);

            Validator.ValidateObject(
                request,
                validationContext,
                validateAllProperties: true);
        }

        private static PermissionResponse MapToResponse(
            Permission permission)
        {
            return new PermissionResponse
            {
                PermissionId = permission.PermissionId,
                PermissionCode = permission.PermissionCode,
                Module = permission.Module,
                PageName = permission.PageName,
                Route = permission.Route,
                Status = permission.Status,
                CreatedDate = permission.CreatedDate,
                ModifiedDate = permission.ModifiedDate,
                RowVersion = permission.RowVersion
            };
        }
    }
}