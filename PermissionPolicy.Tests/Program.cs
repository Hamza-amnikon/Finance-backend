using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using PermissionService.API.Authentication;
using PermissionService.API.CallOtherApis;
using PermissionService.API.DTOs;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Development
});

builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Authentication:UseDevelopmentSession"] = "true",
    ["Authentication:SessionCookieName"] = "Test.Session",
    ["Authentication:ManageRolePermissionsRoles:0"] = "ConfiguredManager"
});

builder.Services.AddFinanceLoginSession(
    builder.Configuration,
    builder.Environment,
    new Uri("https://localhost/"),
    5);

builder.Services.AddSingleton<ILoginSessionClient, TestSessionClient>();

await using var app = builder.Build();
app.UseMiddleware<PermissionRequestFailureMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

var saveCount = 0;

app.MapGet("/assignments", () => Results.Ok(new { loaded = true }))
    .RequireAuthorization("ManageRolePermissions");

app.MapPut("/assignments", () =>
{
    saveCount++;
    return Results.Ok(new { saved = true });
})
.RequireAuthorization("ManageRolePermissions");

app.MapGet("/cancelled-query", (HttpContext context) =>
{
    throw new TaskCanceledException("Simulated database query timeout.");
})
    .RequireAuthorization("ManageRolePermissions");

await app.StartAsync();

var server = app.Services.GetRequiredService<IServer>();
var addresses = server.Features.Get<IServerAddressesFeature>()!;

using var client = new HttpClient(new HttpClientHandler
{
    UseCookies = false
})
{
    BaseAddress = new Uri(addresses.Addresses.Single())
};

foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put })
{
    foreach (var testCase in new[]
    {
        (Cookie: "", Status: HttpStatusCode.Unauthorized),
        (Cookie: "invalid", Status: HttpStatusCode.Unauthorized),
        (Cookie: "viewer", Status: HttpStatusCode.Forbidden),
        (Cookie: "unavailable", Status: HttpStatusCode.ServiceUnavailable),
        (Cookie: "timeout", Status: HttpStatusCode.ServiceUnavailable),
        (Cookie: "manager", Status: HttpStatusCode.OK)
    })
    {
        using var request = new HttpRequestMessage(method, "/assignments");

        if (testCase.Cookie.Length > 0)
        {
            request.Headers.Add("Cookie", $"Test.Session={testCase.Cookie}");
        }

        request.Headers.Add("X-Role", "ConfiguredManager");
        using var response = await client.SendAsync(request);

        if (response.StatusCode != testCase.Status)
        {
            throw new Exception($"{method} {testCase.Cookie}: expected {testCase.Status}, got {response.StatusCode}.");
        }
    }
}

foreach (var testCase in new[]
{
    (Cookie: "manager", Status: HttpStatusCode.OK),
    (Cookie: "viewer", Status: HttpStatusCode.Forbidden),
    (Cookie: "invalid", Status: HttpStatusCode.Unauthorized),
    (Cookie: "unavailable", Status: HttpStatusCode.ServiceUnavailable)
})
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/assignments");
    request.Headers.Add("Cookie", $"Finance.Jwt={testCase.Cookie}; Test.Session=manager");
    request.Headers.Add("X-Finance-Authentication", "microsoft");
    using var response = await client.SendAsync(request);
    if (response.StatusCode != testCase.Status)
    {
        throw new Exception("SSO permissions verification failed or fell back to local admin.");
    }
}

if (saveCount != 1)
{
    throw new Exception("Unauthorized requests reached the save handler.");
}

using (var request = new HttpRequestMessage(HttpMethod.Get, "/cancelled-query"))
{
    request.Headers.Add("Cookie", "Test.Session=manager");
    using var response = await client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (response.StatusCode != HttpStatusCode.GatewayTimeout ||
        response.Headers.CacheControl?.NoStore != true ||
        !body.Contains("permission_timeout", StringComparison.Ordinal))
    {
        throw new Exception("A cancelled database query must return a retryable 504 response.");
    }
}

await app.StopAsync();
Console.WriteLine("PASS: 16 policy checks; only the verified configured manager could load or save. No database records changed.");

public class TestSessionClient : ILoginSessionClient
{
    public Task<LoginSessionResponse?> GetMicrosoftSessionAsync(
        string sessionCookieValue,
        CancellationToken cancellationToken)
    {
        return GetSessionAsync(sessionCookieValue, cancellationToken);
    }

    public Task<LoginSessionResponse?> GetSessionAsync(
        string sessionCookieValue,
        CancellationToken cancellationToken)
    {
        if (sessionCookieValue == "unavailable")
        {
            throw new HttpRequestException("Test service outage.");
        }

        if (sessionCookieValue == "timeout")
        {
            throw new TaskCanceledException("Test timeout.");
        }

        LoginSessionResponse? session = sessionCookieValue switch
        {
            "manager" => new LoginSessionResponse
            {
                Name = "Test manager",
                Role = "ConfiguredManager"
            },
            "viewer" => new LoginSessionResponse
            {
                Name = "Test viewer",
                Role = "Viewer"
            },
            _ => null
        };

        return Task.FromResult(session);
    }
}
