using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RoleService.API.Authentication;

namespace RoleService.API.Services
{
    public class JwtService : IJwtService
    {
        private readonly MeritJwtSettings _settings;
        private readonly EntraSettings _entraSettings;
        private readonly RsaSecurityKey? _signingKey;

        public JwtService(
            IOptions<MeritJwtSettings> settings,
            IOptions<EntraSettings> entraSettings)
        {
            _settings = settings.Value;
            _entraSettings = entraSettings.Value;
            _signingKey = _settings.CreateSigningKey();
        }

        public bool IsConfigured => _signingKey != null;

        public IssuedJwt GenerateToken(ClaimsPrincipal principal)
        {
            if (_signingKey == null || principal.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("MERIT JWT issuance is not configured or the user is not authenticated.");
            }

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_settings.ExpiryMinutes);
            var claims = new[]
            {
                new Claim("sub", principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
                new Claim("name", principal.FindFirstValue(ClaimTypes.Name)!),
                new Claim("email", principal.FindFirstValue(ClaimTypes.Email)!),
                new Claim("role", principal.FindFirstValue(ClaimTypes.Role)!),
                new Claim("role_id", principal.FindFirstValue("role_id")!),
                new Claim("tid", principal.FindFirstValue("tid")!),
                new Claim("oid", principal.FindFirstValue("oid")!),
                new Claim("client_id", _entraSettings.ClientId.ToString()),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(
                    System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
            };
            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

            return new IssuedJwt(new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
}
