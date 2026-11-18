using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoleService.API.Services;

namespace RoleService.API.Authentication
{
    public static class MicrosoftWebAuthenticationRegistration
    {
        public static AuthenticationBuilder AddFinanceMicrosoftWeb(
            this AuthenticationBuilder authentication,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var section = configuration.GetSection("MicrosoftWeb");
            var settings = section.Get<MicrosoftWebSettings>() ?? new MicrosoftWebSettings();

            authentication.Services.AddOptions<MicrosoftWebSettings>()
                .Bind(section)
                .Validate(value => !value.Enabled ||
                    (Uri.TryCreate(value.FrontendUrl, UriKind.Absolute, out var frontend) &&
                    string.IsNullOrEmpty(frontend.UserInfo) &&
                    (frontend.Scheme == Uri.UriSchemeHttps ||
                    (environment.IsDevelopment() && frontend.IsLoopback &&
                    frontend.Scheme == Uri.UriSchemeHttp))),
                    "Configure MicrosoftWeb:FrontendUrl with the trusted frontend URL.")
                .Validate(value => value.CallbackPath.StartsWith('/') &&
                    !value.CallbackPath.StartsWith("//") &&
                    !value.CallbackPath.Contains('?') && !value.CallbackPath.Contains('#'),
                    "Configure MicrosoftWeb:CallbackPath with a local callback path.")
                .ValidateOnStart();

            authentication.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });
            authentication.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
                options.Filters.Add<MicrosoftCookieAntiforgeryFilter>());

            authentication.AddPolicyScheme(
                FinanceAuthenticationSchemes.MicrosoftSession,
                FinanceAuthenticationSchemes.MicrosoftSession,
                options => options.ForwardDefaultSelector = context =>
                    !string.IsNullOrWhiteSpace(context.Request.Headers.Authorization)
                        ? MeritJwtAuthenticationRegistration.SelectBearerScheme(context, configuration["Jwt:Issuer"])
                        : context.Request.Cookies.ContainsKey(MeritJwtCookie.Name)
                            ? FinanceAuthenticationSchemes.MeritBearer
                            : FinanceAuthenticationSchemes.MicrosoftCookie);

            authentication.AddPolicyScheme(
                FinanceAuthenticationSchemes.BrowserSession,
                FinanceAuthenticationSchemes.BrowserSession,
                options => options.ForwardDefaultSelector = context =>
                    context.Request.Cookies.ContainsKey(MeritJwtCookie.Name)
                        ? FinanceAuthenticationSchemes.MeritBearer
                        : FinanceAuthenticationSchemes.MicrosoftCookie);

            authentication.AddCookie(FinanceAuthenticationSchemes.MicrosoftCookie, options =>
            {
                options.Cookie.Name = "Finance.Microsoft";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    var validator = context.HttpContext.RequestServices
                        .GetRequiredService<EntraUserValidator>();
                    var principal = settings.Enabled && context.Principal != null
                        ? await validator.ValidateIdentityAsync(
                            context.Principal,
                            context.HttpContext.RequestAborted)
                        : null;

                    if (principal == null)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(FinanceAuthenticationSchemes.MicrosoftCookie);
                        return;
                    }

                    context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(
                        principal.Claims,
                        FinanceAuthenticationSchemes.MicrosoftCookie,
                        ClaimTypes.Name,
                        ClaimTypes.Role)));
                };
            });

            if (!settings.Enabled)
            {
                return authentication;
            }

            return authentication.AddOpenIdConnect(
                FinanceAuthenticationSchemes.MicrosoftOpenIdConnect,
                options =>
                {
                    options.Authority = configuration["AzureAd:Instance"]!.TrimEnd('/') +
                        "/" + configuration["AzureAd:TenantId"] + "/v2.0";
                    options.ClientId = configuration["AzureAd:ClientId"];
                    options.ClientSecret = configuration["AzureAd:ClientSecret"];
                    options.CallbackPath = settings.CallbackPath;
                    options.SignInScheme = FinanceAuthenticationSchemes.MicrosoftCookie;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.MapInboundClaims = false;
                    options.SaveTokens = false;
                    options.GetClaimsFromUserInfoEndpoint = false;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        context.ProtocolMessage.Prompt = "select_account";
                        return Task.CompletedTask;
                    };
                    options.Events.OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices
                            .GetRequiredService<EntraUserValidator>();
                        var principal = context.Principal == null
                            ? null
                            : await validator.ValidateIdentityAsync(
                                context.Principal,
                                context.HttpContext.RequestAborted);

                        if (principal == null)
                        {
                            context.Properties!.Items["merit_error"] = "not_approved";
                            context.Fail("MERIT access is not approved.");
                            return;
                        }

                        context.Principal = principal;
                    };
                    options.Events.OnRemoteFailure = context =>
                    {
                        var error = context.Properties != null &&
                            context.Properties.Items.TryGetValue("merit_error", out var reason) &&
                            reason == "not_approved"
                            ? "not_approved"
                            : "sign_in_failed";
                        var frontend = new Uri(new Uri(settings.FrontendUrl), "login");
                        context.Response.Redirect(frontend + "?ssoError=" + error);
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                    options.Events.OnTicketReceived = context =>
                    {
                        var jwtService = context.HttpContext.RequestServices.GetRequiredService<IJwtService>();
                        var issued = jwtService.GenerateToken(context.Principal!);

                        context.Response.Cookies.Append(
                            MeritJwtCookie.Name,
                            issued.Token,
                            MeritJwtCookie.CreateOptions(issued.ExpiresUtc));
                        context.Response.Headers.CacheControl = "no-store";
                        context.Response.Headers["Referrer-Policy"] = "no-referrer";
                        context.Response.Redirect(settings.FrontendUrl);
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });
        }
    }
}
