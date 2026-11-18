using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SparkFinance.Authentication;

using var rsa = RSA.Create(2048);
using var otherRsa = RSA.Create(2048);
var issuer = "https://issuer.example.test/";
var audience = "merit-test-api";
var scheme = "TestMeritJwt";
var cookie = "Test.Merit.Jwt";
var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
var checks = 0;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["JwtValidation:Issuer"] = issuer,
    ["JwtValidation:Audience"] = audience,
    ["JwtValidation:PublicKey"] = publicKey,
    ["JwtValidation:CookieName"] = cookie
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    })
    .AddMeritJwtValidation(builder.Configuration.GetSection("JwtValidation"), scheme);
builder.Services.AddAuthorization();

await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/jwt", (ClaimsPrincipal user) => user.Identity?.Name ?? "")
    .RequireAuthorization(policy =>
    {
        policy.AddAuthenticationSchemes(scheme);
        policy.RequireAuthenticatedUser();
    });
app.MapGet("/admin-login", async (HttpContext context) =>
{
    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "Test admin")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
});
app.MapGet("/admin", () => "Admin session preserved").RequireAuthorization();

await app.StartAsync();
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

try
{
    var valid = Token(rsa, issuer, audience, DateTime.UtcNow.AddMinutes(5));
    await CheckRequest("Missing token", null, null, 401);
    await CheckRequest("Valid bearer", valid, null, 200);
    await CheckRequest("Valid cookie", null, valid, 200);
    await CheckRequest("Wrong signature", Token(otherRsa, issuer, audience, DateTime.UtcNow.AddMinutes(5)), null, 401);
    await CheckRequest("Wrong issuer", Token(rsa, "other-issuer", audience, DateTime.UtcNow.AddMinutes(5)), null, 401);
    await CheckRequest("Wrong audience", Token(rsa, issuer, "other-audience", DateTime.UtcNow.AddMinutes(5)), null, 401);
    await CheckRequest("Expired token", Token(rsa, issuer, audience, DateTime.UtcNow.AddMinutes(-1)), null, 401);
    await CheckRequest("Future token", Token(rsa, issuer, audience, DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow.AddMinutes(5)), null, 401);
    await CheckRequest("Unsigned token", Token(null, issuer, audience, DateTime.UtcNow.AddMinutes(5)), null, 401);
    var noExpiry = new JwtSecurityToken(
        issuer,
        audience,
        signingCredentials: new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256));
    await CheckRequest("Missing expiry", new JwtSecurityTokenHandler().WriteToken(noExpiry), null, 401);
    await CheckRequest("Malformed bearer cannot fall back to cookie", "invalid-token", valid, 401);
    await CheckRequest("Invalid bearer cannot fall back to cookie", Token(otherRsa, issuer, audience, DateTime.UtcNow.AddMinutes(5)), valid, 401);

    var wrongAlgorithm = new JwtSecurityToken(
        issuer,
        audience,
        expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)),
            SecurityAlgorithms.HmacSha256));
    await CheckRequest("Wrong algorithm", new JwtSecurityTokenHandler().WriteToken(wrongAlgorithm), null, 401);

    using var login = await client.GetAsync("/admin-login");
    using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/admin");
    adminRequest.Headers.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
    using var adminResponse = await client.SendAsync(adminRequest);
    Check(adminResponse.StatusCode == HttpStatusCode.OK, "Existing default admin cookie scheme is preserved");
    Check(app.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme ==
        CookieAuthenticationDefaults.AuthenticationScheme, "Registration does not replace authentication defaults");

    CheckConfiguration("Missing settings rejected", new JwtValidationSettings());
    CheckConfiguration("Private key rejected", Settings(rsa.ExportRSAPrivateKeyPem()));
    CheckConfiguration("Malformed key rejected", Settings("invalid"));
    using var weakRsa = RSA.Create(1024);
    CheckConfiguration("Weak RSA key rejected", Settings(weakRsa.ExportSubjectPublicKeyInfoPem()));
    var invalidClock = Settings(publicKey);
    invalidClock.ClockSkewSeconds = -1;
    CheckConfiguration("Negative clock skew rejected", invalidClock);
    var parameters = JwtValidationParameters.Create(Settings(publicKey));
    Check(((RsaSecurityKey)parameters.IssuerSigningKey).Parameters.D == null,
        "Validator holds public key material only");

    Console.WriteLine($"Passed {checks} shared JWT validation checks.");
}
finally
{
    await app.StopAsync();
}

async Task CheckRequest(
    string name,
    string? bearer,
    string? cookieToken,
    int status)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/jwt");
    if (bearer != null)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
    }
    if (cookieToken != null)
    {
        request.Headers.Add("Cookie", cookie + "=" + cookieToken);
    }
    using var response = await client.SendAsync(request);
    Check((int)response.StatusCode == status, name);
    if (status == 200)
    {
        Check(await response.Content.ReadAsStringAsync() == "Approved test user", "Name claim is mapped");
    }
}

void CheckConfiguration(string name, JwtValidationSettings settings)
{
    try
    {
        JwtValidationParameters.Create(settings);
    }
    catch (OptionsValidationException)
    {
        Check(true, name);
        return;
    }
    throw new Exception("FAILED: " + name);
}

void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception("FAILED: " + name);
    }
    checks++;
    Console.WriteLine("PASS: " + name);
}

JwtValidationSettings Settings(string key)
{
    return new JwtValidationSettings
    {
        Issuer = issuer,
        Audience = audience,
        PublicKey = key
    };
}

static string Token(
    RSA? key,
    string issuer,
    string audience,
    DateTime expires,
    DateTime? notBefore = null)
{
    var token = new JwtSecurityToken(
        issuer,
        audience,
        [new Claim("sub", Guid.NewGuid().ToString()), new Claim("name", "Approved test user")],
        notBefore ?? DateTime.UtcNow.AddMinutes(-10),
        expires,
        key == null ? null : new SigningCredentials(new RsaSecurityKey(key), SecurityAlgorithms.RsaSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
}
