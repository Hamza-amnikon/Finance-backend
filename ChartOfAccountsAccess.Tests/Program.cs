using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AccountingService.API.Controllers;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
var accounts = new TestAccounts();
builder.Services.AddSingleton<IAccountService>(accounts);
builder.Services.AddSingleton<IChartOfAccountsSetupService>(accounts);
builder.Services.AddSingleton<IAccountingPageAccess, TestAccess>();
builder.Services.AddScoped<AccountsAccessFilter>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);
await using var app = builder.Build();
app.MapControllers();
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(address) };
var checks = 0;
var create = new { companyId = 1, code = "TEST", name = "Test account", type = 0, normalBalance = 0 };
var update = new { code = "TEST", name = "Test account", type = 0, normalBalance = 0, rowVersion = Convert.ToBase64String(new byte[8]) };

try
{
    await Expect(await client.GetAsync("/api/accounts/chart-companies"), 401);
    client.DefaultRequestHeaders.Add("X-Test-User", "ceo");
    var companies = await client.GetFromJsonAsync<List<CompanyChoice>>("/api/accounts/chart-companies");
    Check(companies?.Count == 1 && companies[0].Id == 2 && companies[0].CanCreate && companies[0].CanEdit,
        "CEO company lookup contains only the permitted company and its actions");
    var calls = accounts.Calls;
    foreach (var path in new[]
    {
        "/api/accounts?companyId=1",
        "/api/accounts/tree?companyId=1",
        "/api/accounts/10?companyId=1"
    })
    {
        await Expect(await client.GetAsync(path), 403);
    }
    await Expect(await client.PostAsJsonAsync("/api/accounts", create), 403);
    await Expect(await client.PostAsJsonAsync("/api/accounts/setup", new { companyId = 1 }), 403);
    await Expect(await client.PutAsJsonAsync("/api/accounts/10?companyId=1", update), 403);
    Check(accounts.Calls == calls, "Denied Dubai reads and changes never reach data services");

    await Expect(await client.GetAsync("/api/accounts/tree?companyId=2"), 200);
    await Expect(await client.GetAsync("/api/accounts/10?companyId=2"), 200);
    await Expect(await client.PostAsJsonAsync("/api/accounts", new { companyId = 2, code = "TEST", name = "Test account", type = 0, normalBalance = 0 }), 400);
    Check(true, "Cookie-authenticated changes require CSRF protection");
    var csrf = await client.GetFromJsonAsync<CsrfResult>("/api/accounts/csrf?companyId=2");
    client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.Token);
    await Expect(await client.PostAsJsonAsync("/api/accounts", new { companyId = 2, code = "TEST", name = "Test account", type = 0, normalBalance = 0 }), 201);
    await Expect(await client.PutAsJsonAsync("/api/accounts/10?companyId=2", update), 200);
    await Expect(await client.PostAsJsonAsync("/api/accounts/setup", new { companyId = 2 }), 200);

    client.DefaultRequestHeaders.Remove("X-Test-User");
    client.DefaultRequestHeaders.Add("X-Test-User", "viewer");
    client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
    client.DefaultRequestHeaders.Add("Authorization", "Bearer test-validated-by-fake-access");
    calls = accounts.Calls;
    await Expect(await client.PostAsJsonAsync("/api/accounts", new { companyId = 2, code = "TEST", name = "Test account", type = 0, normalBalance = 0 }), 403);
    await Expect(await client.PutAsJsonAsync("/api/accounts/10?companyId=2", update), 403);
    Check(accounts.Calls == calls, "View-only users cannot create or edit accounts");

    client.DefaultRequestHeaders.Remove("X-Test-User");
    client.DefaultRequestHeaders.Add("X-Test-User", "admin");
    companies = await client.GetFromJsonAsync<List<CompanyChoice>>("/api/accounts/chart-companies");
    Check(companies?.Count == 2, "Local admin retains both companies");
    await Expect(await client.GetAsync("/api/accounts/tree?companyId=1"), 200);
    await Expect(await client.GetAsync("/api/accounts/tree?companyId=2"), 200);
    Console.WriteLine($"Passed {checks} Chart of Accounts integration checks. No database was used.");
}
finally
{
    await app.StopAsync();
}

async Task Expect(HttpResponseMessage response, int status)
{
    using (response)
    {
        Check((int)response.StatusCode == status,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: {status}");
    }
}

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAILED: " + message);
    }
    checks++;
    Console.WriteLine("PASS: " + message);
}

sealed record CompanyChoice(int Id, bool CanCreate, bool CanEdit);
sealed record CsrfResult(string Token);

sealed class TestAccess : IAccountingPageAccess
{
    public Task<LedgerAccess> GetAsync(HttpRequest request, string? permissionCode, CancellationToken cancellationToken)
    {
        var user = request.Headers["X-Test-User"].ToString();
        request.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user)], "Test"));
        return Task.FromResult(user switch
        {
            "admin" => new LedgerAccess(200, true, []),
            "ceo" => new LedgerAccess(200, false, [2]) { CreatableCompanyIds = [2], EditableCompanyIds = [2] },
            "viewer" => new LedgerAccess(200, false, [2]),
            _ => new LedgerAccess(401, false, [])
        });
    }
}

sealed class TestAccounts : IAccountService, IChartOfAccountsSetupService
{
    public int Calls { get; private set; }

    public Task<List<AccountingCompanyLookup>> GetCompaniesAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new List<AccountingCompanyLookup>
        {
            new AccountingCompanyLookup { Id = 1, Name = "Dubai" },
            new AccountingCompanyLookup { Id = 2, Name = "Turbhe" }
        });
    }

    public Task<AccountListDto> GetPagedAsync(AccountQueryDto query, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new AccountListDto());
    }

    public Task<IReadOnlyList<AccountResponseDto>> GetTreeAsync(int companyId, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<AccountResponseDto>>([]);
    }

    public Task<AccountResponseDto?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<AccountResponseDto?>(new AccountResponseDto { Id = id, CompanyId = companyId });
    }

    public Task<AccountResponseDto> CreateAsync(CreateAccountDto dto, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new AccountResponseDto { Id = 10, CompanyId = dto.CompanyId });
    }

    public Task<AccountResponseDto?> UpdateAsync(int id, int companyId, UpdateAccountDto dto, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<AccountResponseDto?>(new AccountResponseDto { Id = id, CompanyId = companyId });
    }

    public Task<IReadOnlyList<AccountResponseDto>> SetupAsync(int companyId, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<AccountResponseDto>>([]);
    }
}
