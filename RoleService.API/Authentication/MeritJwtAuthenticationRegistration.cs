using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RoleService.API.Services;

namespace RoleService.API.Authentication
{
    public static class MeritJwtAuthenticationRegistration
    {
        public static string SelectBearerScheme(HttpContext context, string? meritIssuer)
        {
            var header = context.Request.Headers.Authorization.ToString();

            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var value = header[7..].Trim();
                var handler = new JwtSecurityTokenHandler();

                if (handler.CanReadToken(value))
                {
                    // This only selects a validator. That validator must verify the signature and claims.
                    try
                    {
                        var token = handler.ReadJwtToken(value);

                        if (!string.IsNullOrWhiteSpace(meritIssuer) && token.Issuer == meritIssuer)
                        {
                            return FinanceAuthenticationSchemes.MeritBearer;
                        }
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException or SecurityTokenException or FormatException or
                            System.Text.Json.JsonException)
                    {
                        return FinanceAuthenticationSchemes.MicrosoftBearer;
                    }
                }
            }

            return FinanceAuthenticationSchemes.MicrosoftBearer;
        }

        public static AuthenticationBuilder AddFinanceJwt(
            this AuthenticationBuilder authentication,
            IConfiguration configuration)
        {
            var section = configuration.GetSection("Jwt");
            var settings = section.Get<MeritJwtSettings>() ?? new MeritJwtSettings();
            var signingKey = settings.CreateSigningKey();

            authentication.Services.Configure<MeritJwtSettings>(section);
            authentication.Services.AddSingleton<IJwtService, JwtService>();

            return authentication.AddJwtBearer(FinanceAuthenticationSchemes.MeritBearer, options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = signingKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.Events.OnMessageReceived = context =>
                {
                    if (signingKey == null)
                    {
                        context.Fail("MERIT JWT validation is not configured.");
                    }
                    else if (string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                    {
                        context.Token = context.Request.Cookies[MeritJwtCookie.Name];
                    }

                    return Task.CompletedTask;
                };
                options.Events.OnTokenValidated = async context =>
                {
                    var validator = context.HttpContext.RequestServices.GetRequiredService<EntraUserValidator>();
                    var principal = context.Principal == null
                        ? null
                        : await validator.ValidateIdentityAsync(context.Principal, context.HttpContext.RequestAborted);

                    if (principal == null ||
                        context.Principal!.FindFirstValue("sub") != principal.FindFirstValue(ClaimTypes.NameIdentifier))
                    {
                        context.Fail("MERIT access is not approved for this user and identity.");
                        return;
                    }

                    var scheme = string.IsNullOrWhiteSpace(context.Request.Headers.Authorization)
                        ? FinanceAuthenticationSchemes.MeritCookie
                        : FinanceAuthenticationSchemes.MeritBearer;
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
                        principal.Claims, scheme, ClaimTypes.Name, ClaimTypes.Role));
                };
            });
        }
    }
}
