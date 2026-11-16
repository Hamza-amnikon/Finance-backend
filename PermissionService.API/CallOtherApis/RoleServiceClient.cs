using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PermissionService.API.DTOs;

namespace PermissionService.API.CallOtherApis
{
    public class RoleServiceClient : IRoleServiceClient
    {
        private readonly HttpClient _httpClient;

        public RoleServiceClient(
            HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RoleLookupResponse?> GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roleId),
                    "Select a valid role.");
            }

            using var response = await _httpClient.GetAsync(
                $"api/role/{roleId}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            RoleLookupResponse? role;

            try
            {
                role = await response.Content
                    .ReadFromJsonAsync<RoleLookupResponse>(
                        cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException(
                    "Role Service returned an invalid response.",
                    exception);
            }

            if (role == null ||
                role.RoleId != roleId ||
                string.IsNullOrWhiteSpace(role.Role) ||
                string.IsNullOrWhiteSpace(role.Status))
            {
                throw new HttpRequestException(
                    "Role Service returned incomplete or unexpected role details.");
            }

            return role;
        }
    }
}