using RoleService.API.DTOs;

namespace RoleService.API.Repositories
{
    public interface IUserPagePermissionRepository
    {
        Task<List<UserPagePermissionResponse>> GetByRoleIdAsync(
            int roleId,
            CancellationToken cancellationToken);
    }
}
