using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PermissionService.API.CallOtherApis;

namespace PermissionService.API.Authentication
{
    public class LoginSessionAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "FinanceLoginSession";

        private readonly ILoginSessionClient _sessionClient;
        private readonly LoginSessionSettings _settings;
        private readonly IHostEnvironment _environment;
        private bool _sessionServiceUnavailable;

        public LoginSessionAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ILoginSessionClient sessionClient,
            IOptions<LoginSessionSettings> settings,
            IHostEnvironment environment)
            : base(options, logger, encoder)
        {
            _sessionClient = sessionClient;
            _settings = settings.Value;
            _environment = environment;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!_environment.IsDevelopment() ||
                !_settings.UseDevelopmentSession)
            {
                return AuthenticateResult.NoResult();
            }

            if (!Request.Cookies.TryGetValue(
                _settings.SessionCookieName,
                out var sessionCookieValue))
            {
                return AuthenticateResult.NoResult();
            }

            try
            {
                var session = await _sessionClient.GetSessionAsync(
                    sessionCookieValue,
                    Context.RequestAborted);

                if (session == null)
                {
                    return AuthenticateResult.Fail("The login session is invalid or expired.");
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, session.Name),
                    new Claim(ClaimTypes.Role, session.Role)
                };

                var identity = new ClaimsIdentity(claims, SchemeName);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, SchemeName);

                return AuthenticateResult.Success(ticket);
            }
            catch (HttpRequestException)
            {
                _sessionServiceUnavailable = true;
                return AuthenticateResult.Fail("Login session verification is unavailable.");
            }
            catch (OperationCanceledException)
                when (!Context.RequestAborted.IsCancellationRequested)
            {
                _sessionServiceUnavailable = true;
                return AuthenticateResult.Fail("Login session verification timed out.");
            }
        }

        protected override async Task HandleChallengeAsync(
            AuthenticationProperties properties)
        {
            Response.StatusCode = _sessionServiceUnavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status401Unauthorized;

            await Response.WriteAsJsonAsync(new
            {
                message = _sessionServiceUnavailable
                    ? "Login could not be verified. Check that Role Service is running."
                    : "Sign in before managing role permissions."
            });
        }
    }
}
