using PermissionService.API.DTOs;

namespace PermissionService.API.CallOtherApis
{
    public interface ILoginSessionClient
    {
        Task<LoginSessionResponse?> GetMicrosoftSessionAsync(
            string sessionCookieValue,
            CancellationToken cancellationToken);

        Task<LoginSessionResponse?> GetSessionAsync(
            string sessionCookieValue,
            CancellationToken cancellationToken);
    }
}