using PermissionService.API.Models;

namespace PermissionService.API.Repositories
{
    public interface IRolePermissionRepository
    {
        Task<List<RolePermission>> GetByRoleIdAsync(
            int roleId,
            CancellationToken cancellationToken);

        Task<List<Permission>> GetPermissionsByIdsAsync(
            List<int> permissionIds,
            CancellationToken cancellationToken);

        Task SaveAsync(
            int roleId,
            List<RolePermission> rolePermissions,
            CancellationToken cancellationToken);
    }
}