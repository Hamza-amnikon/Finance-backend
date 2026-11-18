using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace RoleService.API.Authentication
{
    public static class EntraAuthenticationRegistration
    {
        public static IServiceCollection AddFinanceMicrosoftAuthentication(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var section = configuration.GetSection("AzureAd");

            services.AddOptions<EntraSettings>()
                .Bind(section)
                .Validate(settings => settings.TenantId != Guid.Empty,
                    "Configure AzureAd:TenantId.")
                .Validate(settings => settings.ClientId != Guid.Empty,
                    "Configure AzureAd:ClientId with the API application ID.")
                .Validate(settings =>
                    Uri.TryCreate(settings.Instance, UriKind.Absolute, out var instance) &&
                    instance.Scheme == Uri.UriSchemeHttps,
                    "Configure an HTTPS AzureAd:Instance.")
                .Validate(settings => settings.RequiredScopes.All(scope =>
                    !string.IsNullOrWhiteSpace(scope) && !scope.Any(char.IsWhiteSpace)),
                    "AzureAd:RequiredScopes must contain delegated scope names.")
                .Validate(settings => settings.AllowedClientIds.Length > 0 &&
                    settings.AllowedClientIds.All(clientId =>
                        Guid.TryParse(clientId, out var id) && id != Guid.Empty),
                    "Configure AzureAd:AllowedClientIds with the permitted frontend application IDs.")
                .ValidateOnStart();

            services.AddScoped<EntraUserValidator>();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = FinanceAuthenticationSchemes.Combined;
                options.DefaultAuthenticateScheme = FinanceAuthenticationSchemes.Combined;
                options.DefaultChallengeScheme = FinanceAuthenticationSchemes.Combined;
                options.DefaultForbidScheme = FinanceAuthenticationSchemes.Combined;
            })
                .AddPolicyScheme(
                    FinanceAuthenticationSchemes.Combined,
                    FinanceAuthenticationSchemes.Combined,
                    options =>
                    {
                        options.ForwardDefaultSelector = context =>
                        {
                            if (!string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                            {
                                return MeritJwtAuthenticationRegistration.SelectBearerScheme(
                                    context, configuration["Jwt:Issuer"]);
                            }

                            if (context.Request.Cookies.ContainsKey(MeritJwtCookie.Name))
                            {
                                return FinanceAuthenticationSchemes.MeritBearer;
                            }

                            if (configuration.GetValue<bool>("MicrosoftWeb:Enabled") &&
                                context.Request.Cookies.ContainsKey("Finance.Microsoft"))
                            {
                                return FinanceAuthenticationSchemes.MicrosoftCookie;
                            }

                            return environment.IsDevelopment()
                                ? CookieAuthenticationDefaults.AuthenticationScheme
                                : FinanceAuthenticationSchemes.MicrosoftBearer;
                        };
                    })
                .AddFinanceJwt(configuration)
                .AddFinanceMicrosoftWeb(configuration, environment)
                .AddMicrosoftIdentityWebApi(
                    section,
                    jwtBearerScheme: FinanceAuthenticationSchemes.MicrosoftBearer);

            services.Configure<JwtBearerOptions>(
                FinanceAuthenticationSchemes.MicrosoftBearer,
                options =>
                {
                    options.MapInboundClaims = false;
                    options.SaveToken = false;
                    options.IncludeErrorDetails = false;
                    options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
                    options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

                    var validateMicrosoftToken = options.Events.OnTokenValidated;

                    options.Events.OnTokenValidated = async context =>
                    {
                        await validateMicrosoftToken(context);

                        if (context.Result?.Failure != null || context.Principal == null)
                        {
                            return;
                        }

                        var validator = context.HttpContext.RequestServices
                            .GetRequiredService<EntraUserValidator>();

                        var principal = await validator.ValidateAsync(
                            context.Principal,
                            context.HttpContext.RequestAborted);

                        if (principal == null)
                        {
                            context.Fail(
                                "Microsoft sign-in is not approved for this application user or API scope.");
                            return;
                        }

                        context.Principal = principal;
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
