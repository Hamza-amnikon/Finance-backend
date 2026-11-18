using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using AccountingService.API.Authentication;
using AccountingService.API.Data;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SparkFinance.Authentication;

using var rsa = RSA.Create(2048);
var userId = Guid.NewGuid();
var issuer = "https://accounting-jwt.example.test";
var audience = "accounting-tests";
var remote = new SessionHandler();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = "Development"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["JwtValidation:Issuer"] = issuer,
    ["JwtValidation:Audience"] = audience,
    ["JwtValidation:PublicKey"] = rsa.ExportSubjectPublicKeyInfoPem(),
    ["JwtValidation:CookieName"] = "Test.Jwt",
    ["GeneralLedgerAccess:AuthSessionUrl"] = "https://auth.example.test/api/auth/session",
    ["GeneralLedgerAccess:AdminSessionUrl"] = "https://role.example.test/api/development-admin/session"
});
builder.Services.AddAuthentication().AddMeritJwtValidation(
    builder.Configuration.GetSection("JwtValidation"),
    AccountingAuthenticationSchemes.MeritJwt);
builder.Services.AddAuthorization();
builder.Services.AddDbContext<AccountingDbContext>(options =>
{
    // No test is allowed to reach SQL. Early identity failures and admin access must not query it.
    options.UseSqlServer("Server=invalid.example.test;Database=Unused;Integrated Security=true");
});
builder.Services.AddScoped<IGeneralLedgerAccess>(services => new GeneralLedgerAccess(
    new HttpClient(remote, false),
    builder.Configuration,
    builder.Environment,
    services.GetRequiredService<AccountingDbContext>()));

await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/access", async (HttpContext context, IGeneralLedgerAccess access) =>
{
    var result = await access.GetAsync(context.Request, context.RequestAborted);
    return Results.Json(new
    {
        result.IsAdmin,
        allowsDubai = result.Allows(1),
        allowsTurbhe = result.Allows(2)
    }, statusCode: result.Status);
});
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient(new HttpClientHandler { UseCookies = false })
{
    BaseAddress = new Uri(address)
};

try
{
    await Check("Anonymous rejected", null, null, null, 401, 0);
    await Check("Invalid JWT rejected locally", "invalid", null, "microsoft", 401, 0);
    await Check("Admin selector does not grant access", null, null, "admin", 401, 0);
    await Check("Unknown session selector rejected", null, null, "unknown", 401, 0);
    await Check("Admin retains both companies", null, "Finance.DevelopmentAdmin=test-admin", "admin", 200, 1);
    await Check("Admin can coexist with stale Microsoft cookie", null,
        "Finance.DevelopmentAdmin=test-admin; Test.Jwt=invalid", "admin", 200, 1);
    await Check("Microsoft does not fall back to admin", "invalid",
        "Finance.DevelopmentAdmin=test-admin", "microsoft", 401, 0);

    var jwt = new JwtSecurityToken(
        issuer,
        audience,
        [new Claim("sub", userId.ToString())],
        DateTime.UtcNow.AddMinutes(-1),
        DateTime.UtcNow.AddMinutes(5),
        new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
    var token = new JwtSecurityTokenHandler().WriteToken(jwt);

    await Check("Valid bearer still requires active remote identity", token, null, "microsoft", 401, 1);
    await Check("Valid configured cookie still requires active identity", null,
        "Test.Jwt=" + token, "microsoft", 401, 1);
    await Check("Invalid bearer cannot fall back to valid cookie", "invalid",
        "Test.Jwt=" + token, "microsoft", 401, 0);

    remote.ReturnDifferentUser = true;
    await Check("Remote user must match JWT subject", token, null, "microsoft", 401, 1);
    Console.WriteLine("Passed 11 AccountingService JWT/session integration checks.");
}
finally
{
    await app.StopAsync();
}

async Task Check(
    string name,
    string? bearer,
    string? cookie,
    string? method,
    int expectedStatus,
    int expectedCalls)
{
    var previousCalls = remote.Calls;
    using var request = new HttpRequestMessage(HttpMethod.Get, "/access");
    if (bearer != null)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
    }
    if (cookie != null)
    {
        request.Headers.Add("Cookie", cookie);
    }
    if (method != null)
    {
        request.Headers.Add("X-Finance-Authentication", method);
    }
    using var response = await client.SendAsync(request);
    if ((int)response.StatusCode != expectedStatus || remote.Calls - previousCalls != expectedCalls)
    {
        throw new Exception("FAILED: " + name);
    }
    if (expectedStatus == 200)
    {
        var body = await response.Content.ReadFromJsonAsync<AdminAccess>();
        if (body is not { IsAdmin: true, AllowsDubai: true, AllowsTurbhe: true })
        {
            throw new Exception("FAILED admin company access: " + name);
        }
    }
    Console.WriteLine("PASS: " + name);
}

sealed record AdminAccess(bool IsAdmin, bool AllowsDubai, bool AllowsTurbhe);

sealed class SessionHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool ReturnDifferentUser { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Calls++;
        var admin = request.RequestUri!.Host == "role.example.test";
        var response = new HttpResponseMessage(admin || ReturnDifferentUser ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        if (admin)
        {
            response.Content = JsonContent.Create(new { role = "Admin" });
        }
        else if (ReturnDifferentUser)
        {
            response.Content = JsonContent.Create(new { userId = Guid.NewGuid(), roleId = 1, role = "Test" });
        }
        return Task.FromResult(response);
    }
}
