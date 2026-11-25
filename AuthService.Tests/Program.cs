using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AuthService.API;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RoleService.API.Authentication;
using RoleService.API.Models;
using RoleService.API.Repositories;

var tenantId = Guid.NewGuid();
var clientId = Guid.NewGuid();
var issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
using var microsoftRsa = RSA.Create(2048);
using var meritRsa = RSA.Create(2048);
using var tlsRsa = RSA.Create(2048);
var signingKey = new RsaSecurityKey(microsoftRsa) { KeyId = Guid.NewGuid().ToString() };
var request = new CertificateRequest("CN=localhost", tlsRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var names = new SubjectAlternativeNameBuilder();
names.AddIpAddress(IPAddress.Loopback);
names.AddDnsName("localhost");
request.CertificateExtensions.Add(names.Build());
using var generatedCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
using var certificate = X509CertificateLoader.LoadPkcs12(
    generatedCertificate.Export(X509ContentType.Pfx),
    null,
    X509KeyStorageFlags.Exportable);

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
var tokenEndpoint = new WebTokenEndpoint();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Development
});
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options =>
    options.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate)));
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:MySqlConnection"] = "Server=unused;Port=3306;Database=Unused;User ID=test;Password=test;",
    ["Database:MySql:TableMappings:security.Users"] = "Users",
    ["Database:MySql:TableMappings:security.Roles"] = "Roles",
    ["Database:MySql:TableMappings:security.Permissions"] = "Permissions",
    ["Database:MySql:TableMappings:security.RolePermissions"] = "RolePermissions",
    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
    ["AzureAd:TenantId"] = tenantId.ToString(),
    ["AzureAd:ClientId"] = clientId.ToString(),
    ["AzureAd:ClientSecret"] = "isolated-test-credential",
    ["AzureAd:RequiredScopes:0"] = "test_access",
    ["AzureAd:AllowedClientIds:0"] = clientId.ToString(),
    ["MicrosoftWeb:Enabled"] = "true",
    ["MicrosoftWeb:FrontendUrl"] = "http://localhost:5173/",
    ["Jwt:Issuer"] = "MERIT-test",
    ["Jwt:Audience"] = "MERIT-test",
    ["Jwt:ExpiryMinutes"] = "30",
    ["Jwt:PrivateKey"] = meritRsa.ExportPkcs8PrivateKeyPem()
});
builder.Services.AddSingleton<IUserRepository>(users);
builder.Services.AddSingleton<IRoleRepository>(roles);
builder.Services.PostConfigure<OpenIdConnectOptions>(FinanceAuthenticationSchemes.MicrosoftOpenIdConnect, options =>
{
    var metadata = new OpenIdConnectConfiguration
    {
        Issuer = issuer,
        AuthorizationEndpoint = issuer + "/authorize",
        TokenEndpoint = issuer + "/token"
    };
    metadata.SigningKeys.Add(signingKey);
    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
    options.Backchannel = new HttpClient(tokenEndpoint);
});

await using var app = AuthApplication.Build(builder);
await DatabaseProviderChecks.RunAsync(app.Services);
app.MapGet("/test/admin", () => Results.Ok()).RequireAuthorization(policy => policy.RequireRole("Admin"));
await app.StartAsync();

try
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var handler = new HttpClientHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, received, _, _) =>
            received?.Thumbprint == certificate.Thumbprint
    };
    using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };

    foreach (var path in new[] { "api/users", "api/role", "api/development-admin/session" })
    {
        using var response = await client.GetAsync(path);

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            throw new Exception("AuthService exposed a user, role or local Admin endpoint: " + path);
        }
    }

    Console.WriteLine("PASS: The separate AuthService exposes no user, role or local Admin management endpoints.");
    using var anonymous = await client.GetAsync("api/auth/session");

    if (anonymous.StatusCode != HttpStatusCode.Unauthorized)
    {
        throw new Exception("An anonymous AuthService session was accepted.");
    }

    await WebFlowChecks.RunAsync(client, app.Services, tokenEndpoint, users.User,
        signingKey, issuer, clientId, HttpStatusCode.NotFound);
    Console.WriteLine("Standalone AuthService SSO/JWT checks passed over local HTTPS. No database connections were opened.");
}
finally
{
    await app.StopAsync();
}
