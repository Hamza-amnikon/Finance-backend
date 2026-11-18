using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SparkFinance.Authentication
{
    public static class JwtValidationRegistration
    {
        public static AuthenticationBuilder AddMeritJwtValidation(
            this AuthenticationBuilder authentication,
            IConfigurationSection configuration,
            string scheme)
        {
            ArgumentNullException.ThrowIfNull(authentication);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

            var settings = configuration.Get<JwtValidationSettings>()
                ?? new JwtValidationSettings();

            // Validate configuration during registration, before accepting requests.
            var parameters = JwtValidationParameters.Create(settings);

            return authentication.AddJwtBearer(scheme, options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = parameters;
                options.Events.OnMessageReceived = context =>
                {
                    // A supplied Authorization header always takes precedence, even if invalid.
                    if (string.IsNullOrWhiteSpace(context.Request.Headers.Authorization) &&
                        !string.IsNullOrWhiteSpace(settings.CookieName))
                    {
                        context.Token = context.Request.Cookies[settings.CookieName];
                    }

                    return Task.CompletedTask;
                };
            });
        }
    }
}
