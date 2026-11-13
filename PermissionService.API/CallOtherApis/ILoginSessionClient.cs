using PermissionService.API.DTOs;

namespace PermissionService.API.CallOtherApis
{
    public interface ILoginSessionClient
    {
        Task<LoginSessionResponse?> GetSessionAsync(
            string sessionCookieValue,
            CancellationToken cancellationToken);
    }
}