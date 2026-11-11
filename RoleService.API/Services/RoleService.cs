using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Repositories;

namespace RoleService.API.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _repository;

        public RoleService(IRoleRepository repository)
        {
            _repository = repository;
        }

        public Task<List<Roles>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetAllAsync(cancellationToken);
        }

        public Task<List<Roles>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetActiveAsync(cancellationToken);
        }

        public Task<Roles?> GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            return _repository.GetByIdAsync(roleId, cancellationToken);
        }

        public async Task<(RoleChangeResult Result, Roles? Role)> CreateAsync(
            RoleRequest request,
            CancellationToken cancellationToken)
        {
            var role = new Roles
            {
                Role = request.Role.Trim(),
                Status = request.Status
            };

            _repository.Add(role);

            try
            {
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateRole(exception))
            {
                return (RoleChangeResult.DuplicateName, null);
            }

            return (RoleChangeResult.Success, role);
        }

        public async Task<RoleChangeResult> UpdateAsync(
            int roleId,
            RoleRequest request,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            var role = await _repository.GetByIdAsync(roleId, cancellationToken);

            if (role is null)
            {
                return RoleChangeResult.NotFound;
            }

            _repository.SetOriginalRowVersion(role, rowVersion);

            role.Role = request.Role.Trim();
            role.Status = request.Status;
            role.ModifiedDate = DateTime.UtcNow;

            try
            {
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RoleChangeResult.ConcurrencyConflict;
            }
            catch (DbUpdateException exception) when (IsDuplicateRole(exception))
            {
                return RoleChangeResult.DuplicateName;
            }

            return RoleChangeResult.Success;
        }

        public async Task<RoleChangeResult> DeleteAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            var role = await _repository.GetByIdAsync(roleId, cancellationToken);

            if (role is null)
            {
                return RoleChangeResult.NotFound;
            }

            _repository.Remove(role);

            try
            {
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RoleChangeResult.ConcurrencyConflict;
            }

            return RoleChangeResult.Success;
        }

        private static bool IsDuplicateRole(DbUpdateException exception)
        {
            return exception.InnerException is SqlException { Number: 2601 or 2627 };
        }
    }
}
