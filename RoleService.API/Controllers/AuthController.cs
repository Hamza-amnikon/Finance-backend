using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.DTOs;
using RoleService.API.Services;

namespace RoleService.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("login")]
        public async Task<IActionResult> Login(
            [FromServices] IOptions<MicrosoftWebSettings> settings,
            [FromServices] IConfiguration configuration,
            [FromServices] IJwtService jwtService)
        {
            if (!settings.Value.Enabled ||
                string.IsNullOrWhiteSpace(configuration["AzureAd:ClientSecret"]))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Configure Microsoft Web sign-in and AzureAd:ClientSecret in server User Secrets."
                });
            }

            if (!jwtService.IsConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Configure Jwt:Issuer, Jwt:Audience, Jwt:ExpiryMinutes and Jwt:PrivateKey in server configuration."
                });
            }

            MeritJwtCookie.Clear(Response);
            await HttpContext.SignOutAsync(FinanceAuthenticationSchemes.MicrosoftCookie);

            return Challenge(new AuthenticationProperties
            {
                RedirectUri = settings.Value.FrontendUrl
            }, FinanceAuthenticationSchemes.MicrosoftOpenIdConnect);
        }

        [HttpGet("csrf")]
        [Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.BrowserSession)]
        public IActionResult GetCsrf([FromServices] IAntiforgery antiforgery)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(new
            {
                token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
            });
        }

        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.BrowserSession)]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(FinanceAuthenticationSchemes.MicrosoftCookie);
            MeritJwtCookie.Clear(Response);
            return NoContent();
        }

        [HttpGet("session")]
        [Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.MicrosoftSession)]
        public ActionResult<UserSessionResponse> GetSession()
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(new UserSessionResponse
            {
                UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                Name = User.FindFirstValue(ClaimTypes.Name)!,
                Email = User.FindFirstValue(ClaimTypes.Email)!,
                RoleId = int.Parse(
                    User.FindFirstValue("role_id")!,
                    CultureInfo.InvariantCulture),
                Role = User.FindFirstValue(ClaimTypes.Role)!
            });
        }

        [HttpGet("page-permissions")]
        [Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.MicrosoftSession)]
        public async Task<ActionResult<List<PagePermissionResponse>>> GetPagePermissions(
            [FromServices] RoleDbContext db,
            CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";

            if (!int.TryParse(User.FindFirstValue("role_id"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var roleId) || roleId <= 0)
            {
                return Forbid();
            }

            var permissions = await db.Database.SqlQuery<PagePermissionResponse>($"""
                SELECT p.[Route], rp.CompanyId, rp.CanView
                FROM [security].[RolePermissions] AS rp
                INNER JOIN [security].[Permissions] AS p ON p.PermissionId = rp.PermissionId
                WHERE rp.RoleId = {roleId}
                    AND rp.CompanyId > 0
                    AND p.[Status] = 'Active'
                    AND p.[Route] <> ''
                """).ToListAsync(cancellationToken);

            return Ok(permissions);
        }
    }
}
