using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AccountingService.API.Data;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

internal static class SsoAdminAccessChecks
{
    public static async Task RunAsync()
    {
        var userId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GeneralLedgerAccess:AuthSessionUrl"] = "https://identity.test/api/auth/session"
        }).Build();
        using var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new VerifiedToken(userId))
            .BuildServiceProvider();
        using var database = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=Unused;Integrated Security=true;Connect Timeout=1")
            .Options);
        var response = new SessionResponse(userId);
        using var client = new HttpClient(response);
        var access = new GeneralLedgerAccess(client, configuration, new TestEnvironment(), database);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers.Authorization = "Bearer verified-test-token";
        http.Request.Headers["X-Finance-Authentication"] = "microsoft";

        var result = await access.GetAsync(http.Request, "banking.internal-payment-requests", default);
        if (!result.IsAdmin || !result.AllowsCreate(1) || !result.AllowsEdit(2) ||
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) != userId.ToString())
        {
            throw new Exception("SSO admin must receive all-company access while preserving its own actor ID.");
        }

        response.UserId = Guid.NewGuid();
        result = await access.GetAsync(http.Request, null, default);
        if (result.Status != 401 || result.IsAdmin)
        {
            throw new Exception("An Admin session with a different JWT subject must be rejected.");
        }

        response.Status = HttpStatusCode.Unauthorized;
        result = await access.GetAsync(http.Request, null, default);
        if (result.Status != 401 || result.IsAdmin)
        {
            throw new Exception("A rejected SSO session must never receive administrator access.");
        }

        Console.WriteLine("PASS: SSO admin company access, actor identity, mismatched subject and revoked session; no database opened.");
    }

    private sealed class SessionResponse(Guid userId) : HttpMessageHandler
    {
        public Guid UserId { get; set; } = userId;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = JsonContent.Create(new { UserId, RoleId = 1, Role = "Admin", Name = "SSO Admin", IsAdmin = true })
            });
        }
    }

    private sealed class VerifiedToken(Guid userId) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], scheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, scheme!)));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
