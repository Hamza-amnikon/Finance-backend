using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RoleService.API.Models;
using RoleService.API.Repositories;

namespace RoleService.API.Authentication
{
    public class EntraUserValidator
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly EntraSettings _settings;

        public EntraUserValidator(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IOptions<EntraSettings> settings)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _settings = settings.Value;
        }

        public async Task<ClaimsPrincipal?> ValidateAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(principal.FindFirstValue("tid"), out var tenantId) ||
                tenantId == Guid.Empty ||
                tenantId != _settings.TenantId ||
                !Guid.TryParse(principal.FindFirstValue("oid"), out var objectId) ||
                objectId == Guid.Empty)
            {
                return null;
            }

            var scopes = principal.FindAll("scp")
                .SelectMany(claim => claim.Value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries))
                .ToHashSet(StringComparer.Ordinal);

            if (_settings.RequiredScopes.Length == 0 ||
                _settings.RequiredScopes.Any(scope =>
                    string.IsNullOrWhiteSpace(scope) || !scopes.Contains(scope)))
            {
                return null;
            }

            var clientId = principal.FindFirstValue("azp") ??
                principal.FindFirstValue("appid");

            if (_settings.AllowedClientIds.Length == 0 ||
                !Guid.TryParse(clientId, out var callingClientId) ||
                !_settings.AllowedClientIds.Any(allowedClientId =>
                    Guid.TryParse(allowedClientId, out var allowedId) &&
                    allowedId == callingClientId))
            {
                return null;
            }

            var approvedPrincipal = await ValidateIdentityAsync(principal, cancellationToken);

            if (approvedPrincipal?.Identity is ClaimsIdentity approvedIdentity)
            {
                approvedIdentity.AddClaim(new Claim("scp", string.Join(' ', scopes)));
                approvedIdentity.AddClaim(new Claim("azp", callingClientId.ToString()));
            }

            return approvedPrincipal;
        }

        public async Task<ClaimsPrincipal?> ValidateIdentityAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(principal.FindFirstValue("tid"), out var tenantId) ||
                tenantId != _settings.TenantId || tenantId == Guid.Empty ||
                !Guid.TryParse(principal.FindFirstValue("oid"), out var objectId) ||
                objectId == Guid.Empty)
            {
                return null;
            }

            var user = await _userRepository.GetByMicrosoftIdentityAsync(
                tenantId,
                objectId,
                cancellationToken);

            if (user == null || !user.IsActive)
            {
                return null;
            }

            var role = await _roleRepository.GetByIdAsync(
                user.RoleId,
                cancellationToken);

            if (role == null || role.Status != RoleStatus.Active)
            {
                return null;
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, role.Role),
                    new Claim("role_id", role.RoleId.ToString(CultureInfo.InvariantCulture)),
                    new Claim("tid", tenantId.ToString()),
                    new Claim("oid", objectId.ToString())
                },
                FinanceAuthenticationSchemes.MicrosoftBearer,
                ClaimTypes.Name,
                ClaimTypes.Role);

            return new ClaimsPrincipal(identity);
        }
    }
}
