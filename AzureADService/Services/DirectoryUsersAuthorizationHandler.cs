using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using PermissionService.API.Authentication;

namespace AzureADService.Services
{
    public sealed class DirectoryUsersRequirement : IAuthorizationRequirement
    {
    }

    public sealed class DirectoryUsersAuthorizationHandler : AuthorizationHandler<DirectoryUsersRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clients;
        private readonly LoginSessionSettings _settings;

        public DirectoryUsersAuthorizationHandler(
            IHttpContextAccessor httpContextAccessor,
            IHttpClientFactory clients,
            IOptions<LoginSessionSettings> settings)
        {
            _httpContextAccessor = httpContextAccessor;
            _clients = clients;
            _settings = settings.Value;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            DirectoryUsersRequirement requirement)
        {
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return;
            }

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null ||
                !request.Cookies.TryGetValue(_settings.MicrosoftSessionCookieName, out var cookie) ||
                string.IsNullOrWhiteSpace(cookie))
            {
                return;
            }

            using var verification = new HttpRequestMessage(HttpMethod.Get, "api/auth/page-permissions");
            verification.Headers.Add("Cookie", $"{_settings.MicrosoftSessionCookieName}={cookie}");

            using var response = await _clients.CreateClient("DirectoryRoleService")
                .SendAsync(verification, request.HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var grants = await response.Content.ReadFromJsonAsync<List<PageGrant>>(
                cancellationToken: request.HttpContext.RequestAborted);
            if (grants?.Any(grant => grant.PermissionCode == "settings.users" &&
                grant.CanView && grant.CanCreate && grant.CompanyId > 0) == true)
            {
                context.Succeed(requirement);
            }
        }

        private sealed class PageGrant
        {
            public string PermissionCode { get; set; } = string.Empty;
            public int? CompanyId { get; set; }
            public bool CanView { get; set; }
            public bool CanCreate { get; set; }
        }
    }
}
