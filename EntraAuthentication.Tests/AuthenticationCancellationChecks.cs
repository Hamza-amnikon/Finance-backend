using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using RoleService.API.Authentication;
using RoleService.API.Models;

static class AuthenticationCancellationChecks
{
    public static async Task RunAsync(
        IServiceProvider services,
        TestUserRepository users,
        TestRoleRepository roles)
    {
        using var scope = services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<EntraUserValidator>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tid", users.User.TenantId.ToString()),
            new Claim("oid", users.User.ObjectId.ToString())
        ], "Test"));

        using var cancelledBefore = new CancellationTokenSource();
        cancelledBefore.Cancel();
        Check(await validator.ValidateIdentityAsync(principal, cancelledBefore.Token) == null,
            "An already cancelled request cannot authenticate.");

        try
        {
            using var userCancellation = new CancellationTokenSource();
            users.LookupOverride = token =>
            {
                userCancellation.Cancel();
                return Task.FromCanceled<User?>(token);
            };
            Check(await validator.ValidateIdentityAsync(principal, userCancellation.Token) == null,
                "Cancellation during the user lookup is handled without approving an identity.");
            users.LookupOverride = null;

            using var roleCancellation = new CancellationTokenSource();
            roles.LookupOverride = token =>
            {
                roleCancellation.Cancel();
                return Task.FromCanceled<Roles?>(token);
            };
            Check(await validator.ValidateIdentityAsync(principal, roleCancellation.Token) == null,
                "Cancellation during the role lookup is handled without approving an identity.");
            roles.LookupOverride = null;

            users.LookupOverride = token => Task.FromException<User?>(new TaskCanceledException("Provider timeout"));
            var timeoutVisible = false;
            try
            {
                await validator.ValidateIdentityAsync(principal, CancellationToken.None);
            }
            catch (TaskCanceledException)
            {
                timeoutVisible = true;
            }
            Check(timeoutVisible, "An unrelated provider timeout is not hidden as request cancellation.");
            users.LookupOverride = null;

            var http = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                RequestAborted = cancelledBefore.Token
            };
            var scheme = new AuthenticationScheme(
                FinanceAuthenticationSchemes.MicrosoftCookie,
                null,
                typeof(CookieAuthenticationHandler));
            var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(FinanceAuthenticationSchemes.MicrosoftCookie);
            var context = new CookieValidatePrincipalContext(
                http,
                scheme,
                options,
                new AuthenticationTicket(principal, scheme.Name));
            await options.Events.OnValidatePrincipal(context);
            Check(context.Principal == null && !http.Response.Headers.ContainsKey("Set-Cookie"),
                "Cancelling validation rejects only the request and does not clear a valid cookie.");

            Check(await validator.ValidateIdentityAsync(principal, CancellationToken.None) != null,
                "A fresh request still validates the existing active user and role.");
        }
        finally
        {
            users.LookupOverride = null;
            roles.LookupOverride = null;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception("FAILED: " + message);
        }

        Console.WriteLine("PASS: " + message);
    }
}
