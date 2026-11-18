using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using PermissionService.API.CallOtherApis;
using PermissionService.API.Controllers;
using PermissionService.API.Data;
using PermissionService.API.DTOs;
using PermissionService.API.Models;
using PermissionService.API.Repositories;
using PermissionService.API.Services;

var databaseName = "RolePermissionCompanyTests_" + Guid.NewGuid().ToString("N");
var connectionString =
    $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<PermissionDbContext>()
    .UseSqlServer(connectionString)
    .Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddAuthentication();
builder.Services.AddAuthorization(authorization =>
{
    authorization.AddPolicy("ManageRolePermissions", policy =>
    {
        policy.RequireAssertion(_ => true);
    });
});
builder.Services.AddControllers()
    .AddApplicationPart(typeof(RolePermissionsController).Assembly);
builder.Services.AddDbContext<PermissionDbContext>(context =>
{
    context.UseSqlServer(connectionString);
});
builder.Services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddSingleton<IRoleServiceClient, TestRoleClient>();
builder.Services.AddSingleton<ICompanyServiceClient, TestCompanyClient>();

await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

try
{
    await using (var context = new PermissionDbContext(options))
    {
        await context.Database.EnsureCreatedAsync();
        context.Permissions.Add(new Permission
        {
            PermissionCode = "accounting.test",
            Module = "Accounting",
            PageName = "Test",
            Route = "/accounting/test",
            Status = PermissionStatus.Active
        });
        await context.SaveChangesAsync();
    }

    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient
    {
        BaseAddress = new Uri(address)
    };

    await ExpectStatus(await client.GetAsync("api/role-permissions/7"), HttpStatusCode.BadRequest);
    await ExpectStatus(await client.GetAsync("api/role-permissions/7?companyId=0"), HttpStatusCode.BadRequest);
    await ExpectStatus(await client.GetAsync("api/role-permissions/7?companyId=999"), HttpStatusCode.BadRequest);
    await ExpectStatus(await client.PutAsJsonAsync("api/role-permissions/7", new
    {
        roleId = 7,
        permissions = Array.Empty<object>()
    }), HttpStatusCode.BadRequest);
    await Save(101, true);
    await Save(202, false);

    var first = await Load(101);
    var second = await Load(202);
    Check(first.Single().CanEdit && !second.Single().CanEdit,
        "The same role and permission retain different actions in each company.");
    Check(first.Single().CompanyId == 101 && second.Single().CompanyId == 202,
        "Responses identify the requested company.");

    await ExpectStatus(await client.PutAsJsonAsync("api/role-permissions/7", new
    {
        roleId = 7,
        companyId = 101,
        permissions = new[]
        {
            new { permissionId = 1, canView = false, canEdit = true }
        }
    }), HttpStatusCode.BadRequest);
    Check((await Load(101)).Single().CanEdit,
        "Invalid actions do not change saved permissions.");

    await ExpectStatus(await client.PutAsJsonAsync("api/role-permissions/7", new
    {
        roleId = 8,
        companyId = 101,
        permissions = Array.Empty<object>()
    }), HttpStatusCode.BadRequest);

    await ExpectStatus(await client.PutAsJsonAsync("api/role-permissions/7", new
    {
        roleId = 7,
        companyId = 999,
        permissions = Array.Empty<object>()
    }), HttpStatusCode.BadRequest);

    await ExpectStatus(await client.PutAsJsonAsync("api/role-permissions/7", new
    {
        roleId = 7,
        companyId = 101,
        permissions = Array.Empty<object>()
    }), HttpStatusCode.OK);
    Check((await Load(101)).Count == 0,
        "Clearing a company removes only its own assignments.");
    Check((await Load(202)).Single().CanView,
        "Clearing another company leaves this company's assignment intact.");

    await using (var context = new PermissionDbContext(options))
    {
        context.RolePermissions.Add(new RolePermission
        {
            RoleId = 7,
            CompanyId = 202,
            PermissionId = 1,
            CanView = true
        });

        try
        {
            await context.SaveChangesAsync();
            throw new Exception("Duplicate company assignment was accepted.");
        }
        catch (DbUpdateException)
        {
            Console.WriteLine("PASS: Database rejects duplicate role/company/permission assignments.");
        }
    }

    Console.WriteLine("PASS: Company-scoped HTTP load/save and validation checks completed.");

    async Task Save(int companyId, bool canEdit)
    {
        await ExpectStatus(await client.PutAsJsonAsync("api/role-permissions/7", new
        {
            roleId = 7,
            companyId,
            permissions = new[]
            {
                new { permissionId = 1, canView = true, canEdit }
            }
        }), HttpStatusCode.OK);
    }

    async Task<List<RolePermissionResponse>> Load(int companyId)
    {
        return await client.GetFromJsonAsync<List<RolePermissionResponse>>(
            $"api/role-permissions/7?companyId={companyId}") ?? [];
    }
}
finally
{
    await app.StopAsync();
    await using var context = new PermissionDbContext(options);
    await context.Database.EnsureDeletedAsync();
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }

    Console.WriteLine("PASS: " + message);
}

static async Task ExpectStatus(HttpResponseMessage response, HttpStatusCode expected)
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

sealed class TestRoleClient : IRoleServiceClient
{
    public Task<RoleLookupResponse?> GetByIdAsync(
        int roleId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<RoleLookupResponse?>(new RoleLookupResponse
        {
            RoleId = roleId,
            Role = "Test role",
            Status = PermissionStatus.Active
        });
    }
}

sealed class TestCompanyClient : ICompanyServiceClient
{
    public Task<CompanyLookupResponse?> GetByIdAsync(
        int companyId,
        CancellationToken cancellationToken)
    {
        CompanyLookupResponse? company = null;

        if (companyId == 101 || companyId == 202)
        {
            company = new CompanyLookupResponse
            {
                CompanyId = companyId,
                CompanyName = "Test company"
            };
        }

        return Task.FromResult(company);
    }
}
