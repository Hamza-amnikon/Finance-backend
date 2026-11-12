using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace RoleService.API
{
    internal static class DevelopmentAdminEndpoints
    {
        internal const string AdminRole = "Admin";

        public static void MapDevelopmentAdmin(this WebApplication app)
        {
            var routes = app.MapGroup("/api/development-admin");

            routes.MapPost("/login", LoginAsync)
                .RequireRateLimiting("development-login");

            routes.MapGet("/session", (ClaimsPrincipal user) =>
                Results.Ok(new
                {
                    name = user.Identity?.Name,
                    role = AdminRole
                }))
                .RequireAuthorization();

            routes.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            }).RequireAuthorization();
        }

        private static async Task<IResult> LoginAsync(
            LoginRequest request,
            IConfiguration configuration,
            HttpContext context)
        {
            var username = configuration["DevelopmentAdmin:Username"];
            var password = configuration["DevelopmentAdmin:Password"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return Results.Json(
                    new
                    {
                        message = "Set the local admin credentials in RoleService.API User Secrets."
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!string.Equals(username, request.Username, StringComparison.Ordinal)
                || !MatchesPassword(password, request.Password))
            {
                return Results.Json(
                    new
                    {
                        message = "Invalid username or password."
                    },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, AdminRole)
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Results.Ok(new
            {
                name = username,
                role = AdminRole
            });
        }

        private static bool MatchesPassword(string expected, string? supplied)
        {
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty)));
        }

        internal sealed record LoginRequest(string? Username, string? Password);
    }
}
