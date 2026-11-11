using RoleService.API.DTOs;
using RoleService.API.Models;

namespace RoleService.API.Services
{
    public enum RoleChangeResult
    {
        Success,
        NotFound,
        DuplicateName,
        ConcurrencyConflict
    }

    public interface IRoleService
    {
        Task<List<Roles>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<List<Roles>> GetActiveAsync(
            CancellationToken cancellationToken);

        Task<Roles?> GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken);

        Task<(RoleChangeResult Result, Roles? Role)> CreateAsync(
            RoleRequest request,
            CancellationToken cancellationToken);

        Task<RoleChangeResult> UpdateAsync(
            int roleId,
            RoleRequest request,
            byte[] rowVersion,
            CancellationToken cancellationToken);

        Task<RoleChangeResult> DeleteAsync(
            int roleId,
            CancellationToken cancellationToken);
    }
}
