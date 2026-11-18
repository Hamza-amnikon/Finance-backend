using PermissionService.API.Models;

namespace PermissionService.API.Repositories
{
    public interface IRolePermissionRepository
    {
        Task<List<RolePermission>> GetByRoleIdAsync(
            int roleId,
            int companyId,
            CancellationToken cancellationToken);

        Task<List<Permission>> GetPermissionsByIdsAsync(
            List<int> permissionIds,
            CancellationToken cancellationToken);

        Task SaveAsync(
            int roleId,
            int companyId,
            List<RolePermission> rolePermissions,
            CancellationToken cancellationToken);
    }
}
