using RoleService.API.DTOs;

namespace RoleService.API.Services
{
    public interface IUserPagePermissionService
    {
        Task<List<UserPagePermissionResponse>> GetAsync(
            int roleId,
            CancellationToken cancellationToken);
    }
}
