using DashboardService.API.Data;
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
    var dubai = await service.GetSummaryAsync(from, to, "Dubai", "USD", default);
    Check(dubai.AccountsReceivable == 1950m, "Unpaid invoices are not offset by excess credits on another invoice");
    Check(dubai.CustomerCredits == 500m, "Excess payment after credits is shown separately");
    Check(dubai.Revenue == 3150m, "Period invoice sales deduct issued credits and exclude draft and void invoices");
    Check(dubai.Received == 1800m, "Multiple payments are counted once");
    Check(dubai.Expenses == 850m && dubai.Paid == 200m && dubai.AccountsPayable == 500m,
        "Bills, expenses and supplier balances retain their totals and exclude voids and drafts");
    Check(dubai.Customers == 1 && dubai.Vendors == 1, "Active party counts respect branch");

    var all = await service.GetSummaryAsync(from, to, "", "INR", default);
    Check(all.AccountsReceivable == 19800m && all.CustomerCredits == 5000m,
        "Receivables and customer credits convert independently across currencies");
    Check(all.Revenue == 31900m && all.Received == 18100m && all.Expenses == 8620m &&
        all.Paid == 2025m && all.AccountsPayable == 5065m, "Other converted dashboard totals remain correct");

    var turbhe = await service.GetSummaryAsync(from, to, "Turbhe", "INR", default);
    Check(turbhe.AccountsReceivable == 300m && turbhe.CustomerCredits == 0m,
        "Credits from another branch are excluded");
    var september = await service.GetSummaryAsync(new(2026, 9, 1), new(2026, 9, 30), "Dubai", "USD", default);
    Check(september.Revenue == 100m && september.Received == 0m &&
        september.AccountsReceivable == 1950m && september.CustomerCredits == 500m,
        "Period figures follow dates while current balances include all dates");
    var empty = await service.GetSummaryAsync(from, to, "Missing", "INR", default);
    Check(empty.AccountsReceivable == 0m && empty.CustomerCredits == 0m && empty.AccountsPayable == 0m,
        "Empty branch returns zero balances");
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
