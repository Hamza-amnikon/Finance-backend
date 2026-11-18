using System.Security.Claims;

namespace RoleService.API.Services
{
    public record IssuedJwt(string Token, DateTime ExpiresUtc);

    public interface IJwtService
    {
        bool IsConfigured { get; }

        IssuedJwt GenerateToken(ClaimsPrincipal principal);
    }
}
