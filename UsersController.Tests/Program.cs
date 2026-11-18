using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using RoleService.API.Controllers;
using RoleService.API.DTOs;
using RoleService.API.Services;

var service = new TestUserService();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddAuthentication("Test")
    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
        "Test",
        options =>
        {
        });
builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddApplicationPart(typeof(UsersController).Assembly);
builder.Services.AddSingleton<IUserService>(service);

await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();

try
{
    var address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient
    {
        BaseAddress = new Uri(address)
    };
    var userPath = $"api/users/{service.User.UserId}";
    var createRequest = new CreateUserRequest
    {
        TenantId = service.User.TenantId,
        ObjectId = service.User.ObjectId,
        Name = "Test user",
        Email = "user@example.test",
        RoleId = 7
    };
    var updateRequest = new UpdateUserRequest
    {
        Name = "Test user",
        Email = "user@example.test",
        RoleId = 7,
        RowVersion = new byte[8]
    };
    var identityPath = userPath + "/microsoft-identity";
    var identityRequest = new UpdateMicrosoftIdentityRequest
    {
        ObjectId = Guid.NewGuid(),
        RowVersion = new byte[8]
    };

    foreach (var role in new string?[] { null, "Viewer" })
    {
        client.DefaultRequestHeaders.Remove("X-Test-Role");

        if (role != null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        var expected = role == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        await Expect(await client.GetAsync("api/users"), expected);
        await Expect(await client.GetAsync(userPath), expected);
        await Expect(await client.PostAsJsonAsync("api/users", createRequest), expected);
        await Expect(await client.PutAsJsonAsync(userPath, updateRequest), expected);
        await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), expected);
    }

    Check(service.Calls == 0,
        "All five endpoints reject anonymous and non-Admin callers before calling the service.");
    client.DefaultRequestHeaders.Remove("X-Test-Role");
    client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

    await Expect(await client.GetAsync("api/users"), HttpStatusCode.OK);
    await Expect(await client.GetAsync(userPath), HttpStatusCode.OK);

    using (var created = await client.PostAsJsonAsync("api/users", createRequest))
    {
        Check(created.StatusCode == HttpStatusCode.Created &&
            created.Headers.Location?.AbsolutePath == "/" + userPath,
            "Admin creation returns HTTP 201 and a working user resource URL.");
    }

    await Expect(await client.PutAsJsonAsync(userPath, updateRequest), HttpStatusCode.OK);
    Check(true, "Admin callers can list, view and update users.");

    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.OK);
    Check(true, "Only an Admin caller can request a Microsoft identity correction.");

    service.Result = UserChangeResult.DuplicateIdentity;
    await Expect(await client.PostAsJsonAsync("api/users", createRequest), HttpStatusCode.Conflict);
    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.Conflict);
    service.Result = UserChangeResult.ConcurrencyConflict;
    await Expect(await client.PutAsJsonAsync(userPath, updateRequest), HttpStatusCode.Conflict);
    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.Conflict);
    service.Result = UserChangeResult.NotFound;
    await Expect(await client.GetAsync(userPath), HttpStatusCode.NotFound);
    await Expect(await client.PutAsJsonAsync(userPath, updateRequest), HttpStatusCode.NotFound);
    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.NotFound);
    Check(true, "Duplicate identities, stale edits and missing users have the expected HTTP responses.");

    service.Result = UserChangeResult.Success;
    service.FailValidation = true;
    await Expect(await client.PostAsJsonAsync("api/users", createRequest), HttpStatusCode.BadRequest);
    await Expect(await client.PutAsJsonAsync(userPath, updateRequest), HttpStatusCode.BadRequest);
    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.BadRequest);
    service.FailValidation = false;
    Check(true, "Service validation failures return HTTP 400.");

    var callsBeforeInvalidRequests = service.Calls;
    updateRequest.RowVersion = [];
    await Expect(await client.PutAsJsonAsync(userPath, updateRequest), HttpStatusCode.BadRequest);
    createRequest.RoleId = 0;
    await Expect(await client.PostAsJsonAsync("api/users", createRequest), HttpStatusCode.BadRequest);
    identityRequest.RowVersion = [];
    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.BadRequest);
    identityRequest.RowVersion = new byte[9];
    await Expect(await client.PutAsJsonAsync(identityPath, identityRequest), HttpStatusCode.BadRequest);
    await Expect(await client.PutAsync(identityPath, null), HttpStatusCode.UnsupportedMediaType);
    Check(service.Calls == callsBeforeInvalidRequests,
        "Invalid DTOs are rejected before the service is called.");

    Console.WriteLine("User controller checks passed. No database connections were opened.");
}
finally
{
    await app.StopAsync();
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

sealed class TestUserService : IUserService
{
    public int Calls { get; private set; }

    public bool FailValidation { get; set; }

    public UserChangeResult Result { get; set; } = UserChangeResult.Success;

    public UserResponse User { get; } = new UserResponse
    {
        UserId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ObjectId = Guid.NewGuid(),
        Name = "Test user",
        Email = "user@example.test",
        RoleId = 7,
        Role = "Test role",
        RowVersion = new byte[8]
    };

    public Task<List<UserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new List<UserResponse> { User });
    }

    public Task<UserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result == UserChangeResult.NotFound ? null : User);
    }

    public Task<(UserChangeResult Result, UserResponse? User)> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        return Change();
    }

    public Task<(UserChangeResult Result, UserResponse? User)> UpdateAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        return Change();
    }

    public Task<(UserChangeResult Result, UserResponse? User)> UpdateMicrosoftIdentityAsync(
        Guid userId,
        UpdateMicrosoftIdentityRequest request,
        CancellationToken cancellationToken)
    {
        return Change();
    }

    private Task<(UserChangeResult Result, UserResponse? User)> Change()
    {
        Calls++;

        if (FailValidation)
        {
            throw new ValidationException("Select an active role.");
        }

        return Task.FromResult<(UserChangeResult Result, UserResponse? User)>((
            Result,
            Result == UserChangeResult.Success ? User : null));
    }
}

sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();

        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Test"), new Claim(ClaimTypes.Role, role)],
            Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            Scheme.Name)));
    }
}
