using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;
using SalesEstimateService.API.Data;
using SalesEstimateService.API.Models;
using SalesEstimateService.API.Repositories;
using DashboardService.API.Data;

var databaseName = "BranchScopeTests_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
await using var invoices = new SalesInvoiceDbContext(
    new DbContextOptionsBuilder<SalesInvoiceDbContext>().UseSqlServer(connection).Options);
await using var estimates = new SalesEstimateDbContext(
    new DbContextOptionsBuilder<SalesEstimateDbContext>().UseSqlServer(connection).Options);
var created = false;
var checks = 0;

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + message);
    }

    checks++;
    Console.WriteLine("PASS: " + message);
}

try
{
    created = await invoices.Database.EnsureCreatedAsync();

    if (!created)
    {
        throw new Exception("Refusing to reuse an existing database.");
    }

    foreach (var batch in Regex.Split(estimates.Database.GenerateCreateScript(), @"^GO\s*$", RegexOptions.Multiline))
    {
        if (!string.IsNullOrWhiteSpace(batch))
        {
            await estimates.Database.ExecuteSqlRawAsync(batch);
        }
    }

    var date = new DateOnly(2026, 11, 12);
    var clientId = Guid.NewGuid();

    foreach (var branch in new[] { "Office North", "Office South", "Future Office" })
    {
        for (var index = 0; index < 3; index++)
        {
            var invoice = new SalesInvoice
            {
                ClientId = clientId,
                ClientName = "Same customer across branches",
                Branch = branch,
                Currency = "USD",
                InvoiceDate = date,
                DueDate = date.AddDays(7),
                GrandTotal = 105,
                TaxTotal = 5,
                Status = SalesInvoiceStatus.Issued
            };
            invoices.SalesInvoices.Add(invoice);
            invoices.SalesCreditNotes.Add(new SalesCreditNote
            {
                RequestId = Guid.NewGuid(),
                SalesInvoice = invoice,
                CreditNoteDate = date,
                Currency = "USD",
                Reason = "Branch scope test",
                GrandTotal = 10
            });
            estimates.SalesEstimates.Add(new SalesEstimate
            {
                ClientId = clientId,
                ClientName = "Same customer across branches",
                Branch = branch,
                Currency = "USD",
                EstimateDate = date,
                ValidUntil = date.AddDays(7),
                GrandTotal = 105
            });
        }
    }

    await invoices.SaveChangesAsync();
    await estimates.SaveChangesAsync();
    var invoiceRepository = new SalesInvoiceRepository(invoices);
    var creditRepository = new SalesCreditNoteRepository(invoices, new SalesInvoiceService.API.Services.SalesCreditNoteCalculator());
    var estimateRepository = new SalesEstimateRepository(estimates);

    foreach (var branch in new[] { "Office North", "Office South", "Future Office" })
    {
        var invoicePage = await invoiceRepository.GetAllAsync(1, 2, default, branch);
        Check(invoicePage.Total == 3 && invoicePage.Data.Count == 2 &&
            invoicePage.Data.All(row => row.Branch == branch),
            "Invoice totals and first page are scoped before pagination: " + branch);
        var invoiceLast = await invoiceRepository.GetAllAsync(2, 2, default, branch);
        Check(invoiceLast.Data.Count == 1 && invoiceLast.Data[0].Branch == branch,
            "Invoice second page cannot leak another branch: " + branch);
        var creditPage = await creditRepository.GetPagedAsync(1, 2, null, null, default, branch);
        Check(creditPage.Total == 3 && creditPage.Items.Count == 2 &&
            creditPage.Items.All(row => row.SalesInvoice.Branch == branch),
            "Credits follow the original invoice branch before pagination: " + branch);
        var estimatePage = await estimateRepository.GetAllAsync(1, 2, null, null, default, branch);
        Check(estimatePage.Total == 3 && estimatePage.Data.Count == 2 &&
            estimatePage.Data.All(row => row.Branch == branch),
            "Estimate totals and first page are branch scoped: " + branch);
    }

    Check((await invoiceRepository.GetAllAsync(1, 100, default)).Total == 9 &&
        (await creditRepository.GetPagedAsync(1, 100, null, null, default)).Total == 9 &&
        (await estimateRepository.GetAllAsync(1, 100, null, null, default)).Total == 9,
        "All branches preserves the previous list behavior");
    Check((await invoiceRepository.GetAllAsync(1, 100, default, "Missing")).Total == 0 &&
        (await creditRepository.GetPagedAsync(1, 100, null, null, default, "Missing")).Total == 0 &&
        (await estimateRepository.GetAllAsync(1, 100, null, null, default, "Missing")).Total == 0,
        "Unknown branches return no records instead of all records");
    Check((await invoiceRepository.GetAllAsync(1, 100, default, " Office North ")).Total == 3,
        "Surrounding whitespace is normalized without mapping branch names");
    Check(await invoices.SalesInvoices.SumAsync(row => row.GrandTotal) == 945 &&
        await invoices.SalesCreditNotes.SumAsync(row => row.GrandTotal) == 90,
        "Reading scoped lists does not modify financial amounts");

    await invoices.Database.ExecuteSqlRawAsync("""
        EXEC(N'CREATE SCHEMA expenses');
        EXEC(N'CREATE SCHEMA purchases');
        CREATE TABLE expenses.Companies (Id int, Name nvarchar(160));
        CREATE TABLE expenses.Expenses (Id int, Amount decimal(18,2), Currency nvarchar(3), Category nvarchar(50), Supplier nvarchar(100), Date date, Status nvarchar(20), CompanyId int);
        CREATE TABLE purchases.Bills (Id int, GrandTotal decimal(18,2), Currency nvarchar(3), VendorName nvarchar(100), BillDate date, Status nvarchar(20), Branch nvarchar(20));
        CREATE TABLE purchases.PaymentsMade (Id int, Amount decimal(18,2), Currency nvarchar(3), VendorName nvarchar(100), PaymentDate date, Status nvarchar(20), Branch nvarchar(20));
        INSERT INTO expenses.Companies VALUES (71,'Test Company - Office North'),(92,'Test Company - Office South');
        INSERT INTO expenses.Expenses VALUES (1,10,'USD','Test','Same name','2026-11-12','Recorded',71),(2,20,'USD','Test','Same name','2026-11-12','Recorded',92);
        INSERT INTO purchases.Bills VALUES (3,30,'USD','Same name','2026-11-12','Issued','Office South');
        INSERT INTO purchases.PaymentsMade VALUES (4,40,'USD','Same name','2026-11-12','Recorded','Office North');
        """);
    await using var dashboard = new DashboardDbContext(
        new DbContextOptionsBuilder<DashboardDbContext>().UseSqlServer(connection).Options);
    var dashboardService = new DashboardService.API.Services.DashboardService(
        dashboard, null!, NullLogger<DashboardService.API.Services.DashboardService>.Instance);
    var northActivity = await dashboardService.GetActivityAsync(default, "Office North");
    Check(northActivity.Count == 2 && northActivity.All(row => row.Id is "expense-1" or "supplier-payment-4"),
        "Dashboard activity is filtered on the server by Billing Branch");
    var southActivity = await dashboardService.GetActivityAsync(default, "Office South");
    Check(southActivity.Count == 2 && southActivity.All(row => row.Id is "expense-2" or "bill-3"),
        "Expense activity follows CompanyId using the same company filter as dashboard totals");
    Check((await dashboardService.GetActivityAsync(default, "Missing")).Count == 0,
        "Unknown activity branches do not expose other branches");
    Check((await dashboardService.GetActivityAsync(default)).Count == 4,
        "Dashboard All branches retains combined activity");
    Console.WriteLine($"All {checks} branch scope checks passed.");
}
finally
{
    if (created && databaseName.StartsWith("BranchScopeTests_", StringComparison.Ordinal))
    {
        await invoices.Database.EnsureDeletedAsync();
    }
}
