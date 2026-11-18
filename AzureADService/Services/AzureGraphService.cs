using AzureADService.DTOs;

namespace AzureADService.Services
{
    public class AzureGraphService
    {
        private readonly GraphClientFactory _factory;

        public AzureGraphService(GraphClientFactory factory)
        {
            _factory = factory;
        }

        public bool IsConfigured => _factory.Client != null;

        public async Task<DirectoryUserResponse?> GetUserAsync(
            string identity,
            CancellationToken cancellationToken)
        {
            var user = await _factory.Client!.Users[identity].GetAsync(
                request => request.QueryParameters.Select =
                    ["id", "displayName", "mail", "userPrincipalName"],
                cancellationToken);

            if (!Guid.TryParse(user?.Id, out var objectId) || objectId == Guid.Empty)
            {
                return null;
            }

            return new DirectoryUserResponse(
                _factory.TenantId,
                objectId,
                user!.DisplayName,
                user.Mail ?? user.UserPrincipalName,
                user.UserPrincipalName);
        }
    }
}
