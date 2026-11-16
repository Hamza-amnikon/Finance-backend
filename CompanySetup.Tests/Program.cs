using System.ComponentModel.DataAnnotations;
using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using ExpenseService.API.Models;
using ExpenseService.API.Repositories;
using ExpenseService.API.Services;
using Microsoft.EntityFrameworkCore;

var database = "FinanceCompanySetupTests_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<CompanySetupDbContext>()
    .UseSqlServer(connection)
    .Options;
var checks = 0;

try
{
    await using (var context = new TestContext(options))
    {
        await context.Database.EnsureCreatedAsync();
    }

    await using (var context = new TestContext(options))
    {
        var service = Service(context);
        var saved = await service.CreateAsync(Request(), default);
        Check(saved.Currency == "XYZ", "Currency comes from the master");
        Check(saved.CompanyName == "Test Company" && saved.BranchName == "Test Branch",
            "Names are trimmed");
        var branch = await context.Branches.SingleAsync();
        Check(branch.AccountingCompanyId == saved.CompanyId && branch.IsActive,
            "The branch is active and linked to its new company");
        Check(branch.CountryCode == "ZZ" && branch.InvoiceNumberPrefix == "T1",
            "Country and invoice prefix are saved");
        Check((await service.GetAllAsync(default)).Count == 1,
            "Setup is returned by the shared lookup");
    }

    await using (var context = new TestContext(options))
    {
        await Reject<InvalidOperationException>(() => Service(context).CreateAsync(
            Request(branchName: "Another Branch"), default));
    }

    await using (var context = new TestContext(options))
    {
        await Reject<InvalidOperationException>(() => Service(context).CreateAsync(
            Request(companyName: "Another Company"), default));
    }

    await using (var context = new TestContext(options))
    {
        var invalid = Request(companyName: "   ");
        await Reject<ValidationException>(() => Service(context).CreateAsync(invalid, default));
        invalid = Request(companyName: "Invalid Country", branchName: "Invalid Branch");
        invalid.Country = "YY";
        await Reject<ValidationException>(() => Service(context).CreateAsync(invalid, default));
        invalid = Request();
        invalid.InvoiceNumberPrefix = "bad-prefix";
        await Reject<ValidationException>(() => Service(context).CreateAsync(invalid, default));
    }

    await using (var context = new TestContext(options) { FailBranchSave = true })
    {
        await Reject<ValidationException>(() => Service(context).CreateAsync(
            Request(companyName: "Rollback Company", branchName: "Rollback Branch"), default));
    }

    await using (var context = new TestContext(options))
    {
        Check(await context.Companies.CountAsync() == 1 &&
            await context.Branches.CountAsync() == 1,
            "Rejected saves and failed branch inserts leave no partial company or duplicates");
        var original = await context.Companies.SingleAsync();
        Check(original.Name == "Test Company" && original.Country == "ZZ" && original.Currency == "XYZ",
            "Existing company values remain unchanged");
    }

    Console.WriteLine($"Passed {checks} company setup checks using an isolated LocalDB database.");
}
finally
{
    await using var context = new TestContext(options);
    await context.Database.EnsureDeletedAsync();
}

CompanySetupService Service(TestContext context)
{
    return new CompanySetupService(new CompanySetupRepository(context), new Lookup());
}

CompanySetupRequest Request(
    string companyName = " Test Company ",
    string branchName = " Test Branch ")
{
    return new CompanySetupRequest
    {
        CompanyName = companyName,
        BranchName = branchName,
        Country = "ZZ",
        InvoiceNumberPrefix = " T1 "
    };
}

void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new Exception(description);
    }

    checks++;
}

async Task Reject<TException>(Func<Task<CompanySetupResponse>> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        checks++;
        return;
    }

    throw new Exception($"Expected {typeof(TException).Name}.");
}

class TestContext : CompanySetupDbContext
{
    public bool FailBranchSave { get; set; }

    public TestContext(DbContextOptions<CompanySetupDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Company>().ToTable("Companies", "expenses", table =>
            table.ExcludeFromMigrations(false));
        builder.Entity<CompanySetupBranch>().ToTable("Branches", "amnikon", table =>
            table.ExcludeFromMigrations(false));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FailBranchSave && ChangeTracker.Entries<CompanySetupBranch>()
            .Any(entry => entry.State == EntityState.Added))
        {
            throw new ValidationException("Simulated branch insert failure.");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

class Lookup : IExpenseLookupService
{
    public Task<ExpenseCountryLookup> ResolveCountryAsync(string code, CancellationToken token)
    {
        if (code != "ZZ")
        {
            throw new ValidationException("Country is not configured.");
        }

        return Task.FromResult(new ExpenseCountryLookup
        {
            LocationCode = code,
            CountryName = "Test country",
            CurrencyCode = "XYZ"
        });
    }

    public Task<ExpenseCurrencyLookup> ResolveCurrencyAsync(
        string? code, bool requireActive, CancellationToken token)
    {
        return Task.FromResult(new ExpenseCurrencyLookup
        {
            CurrencyCode = code!,
            IsActive = true,
            DecimalPlaces = 2
        });
    }

    public Task<List<ExpenseCountryLookup>> GetCountriesAsync(CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public Task<List<ExpenseCurrencyLookup>> GetCurrenciesAsync(CancellationToken token)
    {
        throw new NotSupportedException();
    }
}
