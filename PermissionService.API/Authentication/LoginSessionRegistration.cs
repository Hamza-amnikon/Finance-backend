using Microsoft.AspNetCore.Authentication;
using PermissionService.API.CallOtherApis;

namespace PermissionService.API.Authentication
{
    public static class LoginSessionRegistration
    {
        public static IServiceCollection AddFinanceLoginSession(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment,
            Uri roleServiceUri,
            int timeoutSeconds)
        {
            var section = configuration.GetSection("Authentication");
            var settings = section.Get<LoginSessionSettings>()
                ?? new LoginSessionSettings();

            services.AddOptions<LoginSessionSettings>()
                .Bind(section)
                .Validate(options => !options.UseDevelopmentSession ||
                    (environment.IsDevelopment() &&
                     roleServiceUri.Scheme == Uri.UriSchemeHttps &&
                     !string.IsNullOrWhiteSpace(options.SessionCookieName) &&
                     options.SessionCookieName.All(character =>
                         char.IsAsciiLetterOrDigit(character) ||
                         character == '.' || character == '-' || character == '_') &&
                     options.ManageRolePermissionsRoles.Length > 0 &&
                     options.ManageRolePermissionsRoles.All(role =>
                         !string.IsNullOrWhiteSpace(role))),
                    "Development session authentication requires Development, HTTPS Role Service, a cookie name and management roles.")
                .ValidateOnStart();

            services.AddHttpClient<ILoginSessionClient, LoginSessionClient>(client =>
            {
                client.BaseAddress = new Uri(
                    roleServiceUri.AbsoluteUri.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            });

            services.AddAuthentication(LoginSessionAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, LoginSessionAuthenticationHandler>(
                    LoginSessionAuthenticationHandler.SchemeName,
                    options =>
                    {
                    });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ManageRolePermissions", policy =>
                {
                    policy.RequireAuthenticatedUser();

                    if (settings.ManageRolePermissionsRoles.Length > 0)
                    {
                        policy.RequireRole(settings.ManageRolePermissionsRoles);
                    }
                    else
                    {
                        policy.RequireAssertion(context => false);
                    }
                });
            });

            return services;
        }
    }
}
