using PermissionService.API.DTOs;

namespace PermissionService.API.Services
{
    public interface IRolePermissionService
    {
        Task<List<RolePermissionResponse>> GetByRoleIdAsync(
            int roleId,
            CancellationToken cancellationToken);

        Task<List<RolePermissionResponse>> SaveAsync(
            SaveRolePermissionsRequest request,
            CancellationToken cancellationToken);
    }
}