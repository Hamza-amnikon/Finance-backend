using PermissionService.API.DTOs;

namespace PermissionService.API.CallOtherApis
{
    public interface IRoleServiceClient
    {
        Task<RoleLookupResponse?> GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken);
    }
}