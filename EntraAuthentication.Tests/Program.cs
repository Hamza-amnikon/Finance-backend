using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RoleService.API.Authentication;
using RoleService.API.Controllers;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Repositories;
using RoleService.API.Services;

var tenantId = Guid.NewGuid();
var apiClientId = Guid.NewGuid();
var frontendClientId = Guid.NewGuid();
var issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
using var rsa = RSA.Create(2048);
var signingKey = new RsaSecurityKey(rsa)
{
    KeyId = Guid.NewGuid().ToString()
};
var users = new TestUserRepository(new User
{
    TenantId = tenantId,
    ObjectId = Guid.NewGuid(),
    Name = "Approved user",
    Email = "approved@example.test",
    RoleId = 14,
    IsActive = true
});
var roles = new TestRoleRepository();
var pagePermissions = new TestPagePermissionService();
using var meritRsa = RSA.Create(2048);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Development
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
    ["AzureAd:TenantId"] = tenantId.ToString(),
    ["AzureAd:ClientId"] = apiClientId.ToString(),
    ["AzureAd:RequiredScopes:0"] = "test_access",
    ["AzureAd:AllowedClientIds:0"] = frontendClientId.ToString(),
    ["AzureAd:ClientSecret"] = "isolated-test-credential",
    ["MicrosoftWeb:Enabled"] = "true",
    ["MicrosoftWeb:FrontendUrl"] = "http://localhost:5173/",
    ["Jwt:Issuer"] = "MERIT-test",
    ["Jwt:Audience"] = "MERIT-test",
    ["Jwt:ExpiryMinutes"] = "30",
    ["Jwt:PrivateKey"] = meritRsa.ExportPkcs8PrivateKeyPem(),
    ["DevelopmentAdmin:Username"] = "test-admin",
    ["DevelopmentAdmin:Password"] = "local-test-password"
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });
builder.Services.AddSingleton<IUserPagePermissionService>(pagePermissions);
builder.Services.AddFinanceMicrosoftAuthentication(builder.Configuration, builder.Environment);
var webTokens = new WebTokenEndpoint();
builder.Services.PostConfigure<OpenIdConnectOptions>(
    FinanceAuthenticationSchemes.MicrosoftOpenIdConnect,
    options =>
    {
        var metadata = new OpenIdConnectConfiguration
        {
            Issuer = issuer,
            AuthorizationEndpoint = issuer + "/authorize",
            TokenEndpoint = issuer + "/token"
        };
        metadata.SigningKeys.Add(signingKey);
        options.ConfigurationManager =
            new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
        options.Backchannel = new HttpClient(webTokens);
    });
builder.Services.AddSingleton<IUserRepository>(users);
builder.Services.AddSingleton<IRoleRepository>(roles);
builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("development-login", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

// Local signing keys and metadata replace network discovery in this test host only.
builder.Services.PostConfigure<JwtBearerOptions>(
    FinanceAuthenticationSchemes.MicrosoftBearer,
    options =>
    {
        var metadata = new OpenIdConnectConfiguration
        {
            Issuer = issuer
        };
        metadata.SigningKeys.Add(signingKey);
        options.ConfigurationManager =
            new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
        options.TokenValidationParameters.IssuerValidator = (value, token, parameters) =>
            value == issuer
                ? value
                : throw new SecurityTokenInvalidIssuerException("Unexpected test issuer.");
    });

await using var app = builder.Build();
// This isolated loopback host simulates HTTPS termination without a local certificate.
app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next(context);
});
app.UseMiddleware<DatabaseAvailabilityMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/test/protected", () => Results.Ok()).RequireAuthorization();
app.MapGet("/test/admin", () => Results.Ok())
    .RequireAuthorization(policy => policy.RequireRole("Admin"));

// Exercise the real legacy endpoints without exposing their internal class publicly.
typeof(AuthController).Assembly.GetType("RoleService.API.DevelopmentAdminEndpoints")!
    .GetMethod("MapDevelopmentAdmin", BindingFlags.Public | BindingFlags.Static)!
    .Invoke(null, [app]);

await app.StartAsync();

try
{
    await AuthenticationCancellationChecks.RunAsync(app.Services, users, roles);
    await DatabaseAvailabilityChecks.RunAsync(app.Services);
    var address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var handler = new HttpClientHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false
    };
    using var client = new HttpClient(handler)
    {
        BaseAddress = new Uri(address)
    };

    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    await Expect(await client.GetAsync("api/auth/page-permissions"), HttpStatusCode.Unauthorized);
    Check(pagePermissions.Calls == 0, "Anonymous requests cannot read page permissions.");
    await Expect(await client.GetAsync("test/protected"), HttpStatusCode.Unauthorized);

    using var login = await client.PostAsJsonAsync("api/development-admin/login", new
    {
        username = "test-admin",
        password = "local-test-password"
    });
    Check(login.StatusCode == HttpStatusCode.OK, "The existing Admin login succeeds.");
    Check(
        login.Headers.GetValues("Set-Cookie").All(value =>
            !value.StartsWith("Finance.Jwt=", StringComparison.Ordinal) &&
            !value.StartsWith("Finance.Microsoft=", StringComparison.Ordinal)),
        "Admin login preserves Microsoft session cookies in other tabs.");
    var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    await Expect(await client.GetAsync("api/development-admin/session"), HttpStatusCode.OK);
    await Expect(await client.GetAsync("test/admin"), HttpStatusCode.OK);
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    Check(true, "The Admin cookie still works and cannot enter the JWT-only session endpoint.");

    client.DefaultRequestHeaders.Remove("Cookie");
    client.DefaultRequestHeaders.Add("Cookie", cookie + "; Finance.Jwt=stale-jwt");
    client.DefaultRequestHeaders.Add("X-Finance-Authentication", "admin");
    await Expect(await client.GetAsync("test/admin"), HttpStatusCode.OK);
    Check(true, "Explicit Admin requests validate the Admin cookie even when a stale Microsoft JWT cookie exists.");
    client.DefaultRequestHeaders.Remove("Cookie");
    await Expect(await client.GetAsync("test/admin"), HttpStatusCode.Unauthorized);
    Check(true, "Selecting Admin authentication cannot grant access without the Admin cookie.");
    client.DefaultRequestHeaders.Remove("X-Finance-Authentication");
    client.DefaultRequestHeaders.Add("Cookie", cookie);

    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid");
    await Expect(await client.GetAsync("test/protected"), HttpStatusCode.Unauthorized);
    Check(true, "An invalid bearer token cannot fall back to a valid Admin cookie.");

    client.DefaultRequestHeaders.Remove("Cookie");
    SetToken(Token());
    using (var session = await client.GetAsync("api/auth/session"))
    {
        Check(session.StatusCode == HttpStatusCode.OK,
            "A correctly signed API access token for an approved active user succeeds.");
        var data = await session.Content.ReadFromJsonAsync<UserSessionResponse>();
        Check(data?.UserId == users.User.UserId && data.RoleId == 14 && data.Role == "Test CA",
            "The session uses the current database user and role, not the incoming Admin claim.");
    }
    await Expect(await client.GetAsync("test/admin"), HttpStatusCode.Forbidden);
    await Expect(await client.GetAsync("api/development-admin/session"), HttpStatusCode.Unauthorized);
    await Expect(await client.GetAsync("api/auth/csrf"), HttpStatusCode.Unauthorized);
    await Expect(await client.PostAsync("api/auth/logout", null), HttpStatusCode.Unauthorized);
    Check(true, "The JWT cannot impersonate the development Admin session or gain an Admin role.");

    using (var response = await client.GetAsync("api/auth/page-permissions?roleId=999"))
    {
        Check(response.StatusCode == HttpStatusCode.OK, "An approved Microsoft user can read navigation permissions.");
        var pages = await response.Content.ReadFromJsonAsync<List<UserPagePermissionResponse>>();
        Check(pagePermissions.LastRoleId == users.User.RoleId && pages?.Single().CompanyId == 2,
            "Page permissions use the verified database role and ignore a supplied role ID.");
        Check(response.Headers.CacheControl?.NoStore == true, "Page permission responses are not cached.");
    }

    roles.RoleName = "Admin";
    using (var response = await client.GetAsync("api/auth/session"))
    {
        var adminSession = await response.Content.ReadFromJsonAsync<UserSessionResponse>();
        Check(adminSession?.IsAdmin == true && adminSession.UserId == users.User.UserId,
            "Database-assigned SSO Admin receives full access without changing identity.");
    }
    await Expect(await client.GetAsync("test/admin"), HttpStatusCode.OK);
    await Expect(await client.GetAsync("api/development-admin/session"), HttpStatusCode.Unauthorized);
    users.User.IsActive = false;
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    users.User.IsActive = true;
    roles.Active = false;
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    roles.Active = true;
    roles.RoleName = "Test CA";
    using (var response = await client.GetAsync("api/auth/session"))
    {
        var revokedSession = await response.Content.ReadFromJsonAsync<UserSessionResponse>();
        Check(revokedSession?.IsAdmin == false, "Removing the Admin role revokes full access on the next request.");
    }
    await Expect(await client.GetAsync("test/admin"), HttpStatusCode.Forbidden);

    users.User.IsActive = false;
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    await Expect(await client.GetAsync("api/auth/page-permissions"), HttpStatusCode.Unauthorized);
    users.User.IsActive = true;
    users.Exists = false;
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    users.Exists = true;
    roles.Active = false;
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    roles.Active = true;
    Check(true, "Inactive users, unknown identities and inactive roles are rejected.");

    foreach (var token in new[]
    {
        Token(audience: Guid.NewGuid().ToString()),
        Token(tokenIssuer: "https://invalid.example.test/"),
        Token(expired: true),
        Token(scope: "another_scope"),
        Token(includeScope: false),
        Token(tokenTenant: Guid.NewGuid().ToString()),
        Token(objectId: ""),
        Token(callingClient: Guid.NewGuid().ToString())
    })
    {
        SetToken(token);
        await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    }
    using (var otherRsa = RSA.Create(2048))
    {
        SetToken(Token(key: new RsaSecurityKey(otherRsa) { KeyId = "untrusted" }));
        await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    }
    Check(true, "Wrong audiences, issuers, signatures, expired tokens, scopes, tenants and clients are rejected.");

    var settings = app.Services.GetRequiredService<IOptions<EntraSettings>>().Value;
    settings.RequiredScopes = [];
    SetToken(Token());
    await Expect(await client.GetAsync("api/auth/session"), HttpStatusCode.Unauthorized);
    client.DefaultRequestHeaders.Authorization = null;
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    await Expect(await client.GetAsync("api/development-admin/session"), HttpStatusCode.OK);
    await Expect(await client.PostAsync("api/development-admin/logout", null), HttpStatusCode.NoContent);
    Check(true, "An unconfigured API scope blocks JWT access while existing cookie session and logout still work.");

    client.DefaultRequestHeaders.Clear();
    await WebFlowChecks.RunAsync(
        client,
        app.Services,
        webTokens,
        users.User,
        signingKey,
        issuer,
        apiClientId);

    Console.WriteLine("Web OIDC, cookie and JWT authentication checks passed. No database connections were opened.");

    void SetToken(string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
finally
{
    await app.StopAsync();
}

string Token(
    string? audience = null,
    string? tokenIssuer = null,
    bool expired = false,
    string scope = "test_access",
    bool includeScope = true,
    string? tokenTenant = null,
    string? objectId = null,
    string? callingClient = null,
    SecurityKey? key = null)
{
    var claims = new List<Claim>
    {
        new("tid", tokenTenant ?? tenantId.ToString()),
        new("oid", objectId ?? users.User.ObjectId.ToString()),
        new("azp", callingClient ?? frontendClientId.ToString()),
        new("ver", "2.0"),
        new("roles", "Admin"),
        new("name", "Untrusted token name")
    };

    if (includeScope)
    {
        claims.Add(new Claim("scp", scope));
    }

    var token = new JwtSecurityToken(
        issuer: tokenIssuer ?? issuer,
        audience: audience ?? apiClientId.ToString(),
        claims: claims,
        notBefore: DateTime.UtcNow.AddHours(-2),
        expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(10),
        signingCredentials: new SigningCredentials(
            key ?? signingKey,
            SecurityAlgorithms.RsaSha256));

    return new JwtSecurityTokenHandler().WriteToken(token);
}

static async Task Expect(HttpResponseMessage response, HttpStatusCode expected)
{
    using (response)
    {
        if (response.StatusCode != expected)
        {
            throw new Exception(
                $"Expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }

    Console.WriteLine("PASS: " + message);
}

