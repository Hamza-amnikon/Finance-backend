using System.Net;
using System.Net.Http.Json;
using DashboardService.API.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using DashboardService.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// Only use a new, uniquely named test database; never write fixtures to SparkFinance.
var connection = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("DASHBOARD_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB",
    InitialCatalog = "FinanceDashboardChecks_" + Guid.NewGuid().ToString("N"),
    IntegratedSecurity = true,
    TrustServerCertificate = true
};
await using var context = new DashboardDbContext(
    new DbContextOptionsBuilder<DashboardDbContext>().UseSqlServer(connection.ConnectionString).Options);
var created = false;
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception("FAILED: " + name);
    }

    checks++;
    Console.WriteLine("PASS: " + name);
}

try
{
    created = await context.Database.EnsureCreatedAsync();
    if (!created)
    {
        throw new Exception("Refusing to use an existing test database.");
    }

    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA sales");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA purchases");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA expenses");
    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE sales.SalesInvoices (Id int PRIMARY KEY, GrandTotal decimal(18,4), Currency nvarchar(3), Status nvarchar(20), InvoiceDate date, Branch nvarchar(20));
        CREATE TABLE sales.SalesInvoicePayments (SalesInvoiceId int, Amount decimal(18,4), Currency nvarchar(3), PaymentDate date, Status nvarchar(20) NOT NULL DEFAULT 'Recorded');
        CREATE TABLE sales.SalesCreditNotes (SalesInvoiceId int, GrandTotal decimal(18,4), Currency nvarchar(3), Status nvarchar(20), CreditNoteDate date);
        CREATE TABLE purchases.Bills (Id int PRIMARY KEY, GrandTotal decimal(18,4), Currency nvarchar(3), Status nvarchar(20), BillDate date, Branch nvarchar(20));
        CREATE TABLE purchases.PaymentsMade (BillId int, Amount decimal(18,4), Currency nvarchar(3), Status nvarchar(20), PaymentDate date, Branch nvarchar(20));
        CREATE TABLE purchases.VendorCreditApplications (BillId int, Amount decimal(18,4), Status nvarchar(20));
        CREATE TABLE expenses.Companies (Id int PRIMARY KEY, Name nvarchar(160));
        CREATE TABLE expenses.Expenses (CompanyId int, Amount decimal(18,4), Currency nvarchar(3), Status nvarchar(20), Date date);
        CREATE TABLE dbo.ClientMaster (Branch nvarchar(20), IsActive bit);
        CREATE TABLE purchases.VendorMaster (Branch nvarchar(20), IsActive bit);

        INSERT INTO sales.SalesInvoices VALUES
          (1,1000,'USD','Issued','2026-10-10','Dubai'),
          (2,2000,'USD','Issued','2026-10-10','Dubai'),
          (3,500,'USD','Issued','2026-10-10','Dubai'),
          (4,700,'USD','Issued','2026-10-10','Dubai'),
          (5,900,'USD','Draft','2026-10-10','Dubai'),
          (6,800,'USD','Void','2026-10-10','Dubai'),
          (7,400,'INR','Issued','2026-10-10','Turbhe'),
          (8,100,'USD','Issued','2026-09-10','Dubai');
        INSERT INTO sales.SalesInvoicePayments (SalesInvoiceId,Amount,Currency,PaymentDate,Status) VALUES(1,999999,'AED','2026-10-10','Voided');
        INSERT INTO sales.SalesInvoicePayments (SalesInvoiceId,Amount,Currency,PaymentDate) VALUES
          (1,600,'USD','2026-10-11'),(2,300,'USD','2026-10-11'),
          (2,200,'USD','2026-10-12'),(4,700,'USD','2026-10-11'),(7,100,'INR','2026-10-11');
        INSERT INTO sales.SalesCreditNotes VALUES
          (1,900,'USD','Issued','2026-10-12'),(2,100,'USD','Issued','2026-10-12'),
          (2,50,'USD','Issued','2026-10-13'),(2,999,'USD','Draft','2026-10-13');
        INSERT INTO purchases.Bills VALUES
          (1,800,'USD','Issued','2026-10-10','Dubai'),(2,100,'INR','Issued','2026-10-10','Turbhe'),
          (3,999,'USD','Draft','2026-10-10','Dubai');
        INSERT INTO purchases.PaymentsMade VALUES
          (1,200,'USD','Recorded','2026-10-11','Dubai'),(1,90,'USD','Voided','2026-10-11','Dubai'),
          (2,25,'INR','Recorded','2026-10-11','Turbhe');
        INSERT INTO purchases.VendorCreditApplications VALUES (1,100,'Applied'),(1,90,'Voided'),(2,10,'Applied');
        INSERT INTO expenses.Companies VALUES (1,'Company - Dubai'),(2,'Company - Turbhe');
        INSERT INTO expenses.Expenses VALUES
          (1,50,'USD','Recorded','2026-10-10'),(2,20,'INR','Recorded','2026-10-10'),
          (1,999,'USD','Draft','2026-10-10');
        INSERT INTO dbo.ClientMaster VALUES ('Dubai',1),('Dubai',0),('Turbhe',1);
        INSERT INTO purchases.VendorMaster VALUES ('Dubai',1),('Turbhe',1);
        """);

    var service = new DashboardService.API.Services.DashboardService(
        context, new FixedRates(), NullLogger<DashboardService.API.Services.DashboardService>.Instance);
    var from = new DateOnly(2026, 10, 1);
    var to = new DateOnly(2026, 10, 27);
    DashboardBranch[] bothCompanies = [
        new DashboardBranch(1, 1, "Dubai", "USD"),
        new DashboardBranch(2, 2, "Turbhe", "INR")
    ];
    var dubai = await service.GetSummaryAsync(from, to, "Dubai", "USD", default, bothCompanies);
    Check(dubai.AccountsReceivable == 1950m, "Unpaid invoices are not offset by excess credits on another invoice");
    Check(dubai.CustomerCredits == 500m, "Excess payment after credits is shown separately");
    Check(dubai.Revenue == 3150m, "Period invoice sales deduct issued credits and exclude draft and void invoices");
    Check(dubai.Received == 1800m, "Multiple payments are counted once");
    Check(dubai.Expenses == 850m && dubai.Paid == 200m && dubai.AccountsPayable == 500m,
        "Bills, expenses and supplier balances retain their totals and exclude voids and drafts");
    Check(dubai.Customers == 1 && dubai.Vendors == 1, "Active party counts respect branch");

    var all = await service.GetSummaryAsync(from, to, "", "INR", default, bothCompanies);
    Check(all.AccountsReceivable == 19800m && all.CustomerCredits == 5000m,
        "Receivables and customer credits convert independently across currencies");
    Check(all.Revenue == 31900m && all.Received == 18100m && all.Expenses == 8620m &&
        all.Paid == 2025m && all.AccountsPayable == 5065m, "Other converted dashboard totals remain correct");

    var turbhe = await service.GetSummaryAsync(from, to, "Turbhe", "INR", default, bothCompanies);
    Check(turbhe.AccountsReceivable == 300m && turbhe.CustomerCredits == 0m,
        "Credits from another branch are excluded");
    var september = await service.GetSummaryAsync(new(2026, 9, 1), new(2026, 9, 30), "Dubai", "USD", default, bothCompanies);
    Check(september.Revenue == 100m && september.Received == 0m &&
        september.AccountsReceivable == 1950m && september.CustomerCredits == 500m,
        "Period figures follow dates while current balances include all dates");
    var empty = await service.GetSummaryAsync(from, to, "Missing", "INR", default, bothCompanies);
    Check(empty.AccountsReceivable == 0m && empty.CustomerCredits == 0m && empty.AccountsPayable == 0m,
        "Empty branch returns zero balances");
    DashboardBranch[] turbheScope = [new DashboardBranch(2, 2, "Turbhe", "INR")];
    var scoped = await service.GetSummaryAsync(from, to, "", "INR", default, turbheScope);
    Check(scoped.Revenue == 400m && scoped.Received == 100m && scoped.Expenses == 120m &&
        scoped.Paid == 25m && scoped.AccountsReceivable == 300m && scoped.AccountsPayable == 65m &&
        scoped.CustomerCredits == 0m && scoped.Customers == 1 && scoped.Vendors == 1,
        "All branches applies the authorized company scope to every total and count");

    await context.Database.ExecuteSqlRawAsync("""
        ALTER TABLE sales.SalesInvoices ADD ClientName nvarchar(100) NOT NULL DEFAULT N'Client';
        ALTER TABLE sales.SalesInvoicePayments ADD Id int IDENTITY;
        ALTER TABLE purchases.Bills ADD VendorName nvarchar(100) NOT NULL DEFAULT N'Vendor';
        ALTER TABLE purchases.PaymentsMade ADD Id int IDENTITY, VendorName nvarchar(100) NOT NULL DEFAULT N'Vendor';
        ALTER TABLE expenses.Expenses ADD Id int IDENTITY, Category nvarchar(100) NOT NULL DEFAULT N'Category',
            Supplier nvarchar(100) NOT NULL DEFAULT N'Supplier';
        """);
    var activity = await service.GetActivityAsync(default, null, turbheScope);
    Check(activity.Count == 4 && activity.All(item => item.Action.Contains("INR", StringComparison.Ordinal)),
        "Recent activity excludes the other company");

    var noAccess = await service.GetSummaryAsync(from, to, "", "INR", default, []);
    Check(noAccess.Revenue == 0m && noAccess.Expenses == 0m && noAccess.Customers == 0 &&
        noAccess.Vendors == 0, "An empty authorized scope never becomes all companies");

    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA amnikon");
    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE amnikon.Branches (Id int PRIMARY KEY, Name nvarchar(20), AccountingCompanyId int, IsActive bit);
        INSERT INTO amnikon.Branches VALUES (1,N'Dubai',1,1),(2,N'Turbhe',2,1),(3,N'Inactive',2,0);
        """);
    var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DashboardAccess:CompaniesUrl"] = "https://accounting.test/api/accounts/dashboard-companies"
    }).Build();
    using var handler = new CompanyAccessHandler();
    using var client = new HttpClient(handler);
    var access = new DashboardAccessService(client, settings, context);
    var request = new DefaultHttpContext().Request;

    var unauthorized = await access.GetAsync(request, default);
    Check(unauthorized.Status == 401 && unauthorized.Branches.Count == 0,
        "An unauthenticated session receives no Dashboard branch metadata");

    handler.ResponseStatus = HttpStatusCode.OK;
    handler.Companies = [new DashboardCompany(2, "INR")];
    var restricted = await access.GetAsync(request, default);
    Check(restricted.Status == 200 && restricted.Branches.Select(branch => branch.BranchId).SequenceEqual([2]),
        "Company verification maps only permitted active branches");

    handler.Companies = [new DashboardCompany(1, "USD"), new DashboardCompany(2, "INR")];
    var administrator = await access.GetAsync(request, default);
    Check(administrator.Status == 200 && administrator.Branches.Count == 2,
        "Verification for an administrator includes both active branches");

    Console.WriteLine($"{checks} dashboard checks passed.");
}
finally
{
    if (created)
    {
        await context.Database.EnsureDeletedAsync();
    }
}

sealed class FixedRates : IDashboardExchangeRateService
{
    public Task<DashboardExchangeRateDto> GetRateAsync(string source, string target, DateOnly? date, CancellationToken cancellationToken)
    {
        var inr = new Dictionary<string, decimal> { ["INR"] = 1m, ["USD"] = 10m };
        return Task.FromResult(new DashboardExchangeRateDto
        {
            Rate = inr[source] / inr[target],
            RateDate = date!.Value,
            Source = "Test rates",
            RetrievedAtUtc = DateTimeOffset.UtcNow
        });
    }
}

sealed class CompanyAccessHandler : HttpMessageHandler
{
    public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.Unauthorized;

    public DashboardCompany[] Companies { get; set; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(ResponseStatus)
        {
            Content = JsonContent.Create(Companies)
        });
    }
}
