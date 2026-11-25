using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RoleService.API.Authentication;
using RoleService.API.DTOs;
using RoleService.API.Services;

namespace RoleService.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        [HttpGet("page-permissions")]
        [Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.MicrosoftSession)]
        public async Task<IActionResult> GetPagePermissions(
            [FromServices] IUserPagePermissionService permissions,
            [FromServices] ILogger<AuthController> logger,
            CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";

            if (!int.TryParse(User.FindFirstValue("role_id"), out var roleId) || roleId <= 0)
            {
                return Unauthorized();
            }

            try
            {
                return Ok(await permissions.GetAsync(roleId, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception exception) when (DatabaseAvailabilityMiddleware.IsDatabaseFailure(exception))
            {
                logger.LogError(exception, "Could not load the signed-in user's page permissions.");
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Page permissions could not be loaded. Please retry.");
            }
        }

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
                IsAdmin = User.IsInRole(FinanceRoleNames.Admin),
                UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                Name = User.FindFirstValue(ClaimTypes.Name)!,
                Email = User.FindFirstValue(ClaimTypes.Email)!,
                RoleId = int.Parse(
                    User.FindFirstValue("role_id")!,
                    CultureInfo.InvariantCulture),
                Role = User.FindFirstValue(ClaimTypes.Role)!
            });
        }

    }
}
