using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;
using SalesInvoiceService.API.Services;

var recover = args.Contains("--recover-authorized-six-test-payments");
var database = recover ? "SparkFinance" : "ReceivedVoidTests_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var context = new SalesInvoiceDbContext(new DbContextOptionsBuilder<SalesInvoiceDbContext>()
    .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options);
var created = false;
var checks = 0;
void Check(bool valid, string description)
{
    if (!valid) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
    checks++;
}
async Task Reject(Func<Task> action, string description)
{
    try { await action(); }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    {
        Check(true, description);
        return;
    }
    throw new Exception("FAIL: " + description);
}
try
{
    var service = new ReceivedPaymentVoidService(context);
    if (recover)
    {
        var ids = Enumerable.Range(2029, 6).ToArray();
        var records = await context.SalesInvoicePayments.IgnoreQueryFilters()
            .Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToListAsync();
        Check(records.Count == 6 && records.All(row => row.Amount == 85m &&
            row.Currency == "USD" && row.PaymentMode == "Cheque" &&
            row.SalesInvoiceId == 2029 && row.Reference.StartsWith("TEST-INVALID-")),
            "Recovery matches exactly the six authorized test payments");
        var countBefore = await context.SalesInvoicePayments.IgnoreQueryFilters().CountAsync();
        Directory.CreateDirectory("artifacts");
        var checkpoint = "artifacts/received-test-payments-2029-2034-before.json";
        if (!File.Exists(checkpoint))
        {
            await File.WriteAllTextAsync(checkpoint, JsonSerializer.Serialize(records.Select(row => new
            {
                row.Id, row.SalesInvoiceId, row.RequestId, row.Amount, row.Currency,
                row.PaymentMode, row.Reference, row.BankAmount, row.BankChargeAmount,
                row.Status, row.CreatedDate
            }), new JsonSerializerOptions { WriteIndented = true }));
        }
        // A single outer batch is not used: each service call is independently atomic and replay safe.
        foreach (var id in ids)
        {
            var result = await service.VoidAsync(id,
                "Test payment cancelled at user request; no actual customer receipt.", CancellationToken.None);
            Check(result.Status == "Voided", "Voided existing test payment " + id);
            context.ChangeTracker.Clear();
        }
        Check(await context.SalesInvoicePayments.IgnoreQueryFilters().CountAsync() == countBefore,
            "Payment row count unchanged; no deletions or duplicate payments");
        var totals = await context.SalesInvoicePayments.Where(row => row.SalesInvoiceId == 2029)
            .SumAsync(row => row.Amount);
        Console.WriteLine("Invoice 2029 remaining recorded receipts: USD " + totals);
        var liveSummaryService = new SalesInvoicePaymentService(new SalesInvoiceRepository(context),
            new SalesInvoicePaymentRepository(context), null!, null!);
        var liveSummary = await liveSummaryService.GetSummaryAsync(2029, CancellationToken.None);
        Console.WriteLine($"Invoice total: {liveSummary!.InvoiceTotal}; paid: {liveSummary.PaidAmount}; balance: {liveSummary.BalanceDue}");
        await using var accounting = new AccountingService.API.Data.AccountingDbContext(
            new DbContextOptionsBuilder<AccountingService.API.Data.AccountingDbContext>().UseSqlServer(connection).Options);
        var report = await new AccountingService.API.Services.ReconciliationReportService(accounting)
            .GetAsync(1, DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        Check(!report.Checks.Any(row => row.Title == "Customer payment" &&
            ids.Any(id => row.Reference == "Payment " + id)),
            "Updated reconciliation excludes all six voided receipts");
        var cancelledDrafts = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM banking.BankTransactions WHERE Id BETWEEN 76 AND 81 AND Status='Cancelled'").SingleAsync();
        Check(cancelledDrafts == 6, "All six existing bank drafts are cancelled");
        var journalCount = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM accounting.JournalEntries WHERE Source='PaymentReceived' AND SourceDocumentId IN ('2029','2030','2031','2032','2033','2034')").SingleAsync();
        Check(journalCount == 0, "No accounting journals created for the test receipts");
        await File.WriteAllTextAsync("artifacts/received-test-payments-2029-2034-after.json",
            JsonSerializer.Serialize(new
            {
                PaymentIds = ids, Status = "Voided", CancelledDrafts = cancelledDrafts,
                JournalCount = journalCount, PaymentRowCount = countBefore,
                liveSummary.InvoiceTotal, liveSummary.PaidAmount, liveSummary.BalanceDue,
                ReconciliationIssues = report.Checks.Where(row => row.Status == "Issue").Select(row => new { row.Title, row.Reference, row.Explanation })
            }, new JsonSerializerOptions { WriteIndented = true }));
        return;
    }

    created = await context.Database.EnsureCreatedAsync();
    if (!created) throw new Exception("Refusing to reuse a test database.");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA banking");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA accounting");
    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE banking.PaymentBankingLinks(
            Id int IDENTITY PRIMARY KEY, Source nvarchar(20), SourcePaymentId int,
            BankTransactionId int NULL, ReversalTransactionId int NULL, AccountingJournalEntryId int NULL,
            State nvarchar(20), AccountingState nvarchar(20), Message nvarchar(1000),
            AccountingMessage nvarchar(1000), UpdatedAtUtc datetime2, NextAttemptAtUtc datetime2);
        CREATE TABLE banking.BankTransactions(Id int PRIMARY KEY, Status nvarchar(20),
            CancelledDate datetime2 NULL, ModifiedDate datetime2 NULL, Notes nvarchar(2000));
        CREATE TABLE accounting.JournalEntries(Id int PRIMARY KEY, Source nvarchar(40), SourceDocumentId nvarchar(100));
        """);
    var invoice = new SalesInvoice
    {
        ClientId = Guid.NewGuid(), ClientName = "Test", Currency = "USD", Branch = "Dubai",
        Status = SalesInvoiceStatus.Issued, GrandTotal = 1000m,
        InvoiceDate = new DateOnly(2026,11,9), DueDate = new DateOnly(2026,11,12)
    };
    context.SalesInvoices.Add(invoice);
    await context.SaveChangesAsync();
    for (var id = 1; id <= 5; id++)
    {
        context.SalesInvoicePayments.Add(new SalesInvoicePayment
        {
            SalesInvoiceId = invoice.Id, RequestId = Guid.NewGuid(), Amount = 85m,
            Currency = "USD", PaymentMode = "Cheque", Reference = "TEST-" + id,
            PaymentDate = new DateOnly(2026,11,9)
        });
    }
    await context.SaveChangesAsync();
    await context.Database.ExecuteSqlRawAsync("""
        INSERT INTO banking.BankTransactions(Id,Status,Notes) VALUES(1,'Draft',''),(2,'Posted','');
        INSERT INTO banking.PaymentBankingLinks(Source,SourcePaymentId,BankTransactionId,State,AccountingState)
        VALUES('Received',1,1,'Draft','Pending'),('Received',2,2,'Posted','Pending');
        INSERT INTO accounting.JournalEntries VALUES(1,'PaymentReceived','3');
        """);
    context.ChangeTracker.Clear();
    await Reject(() => service.VoidAsync(1, "", CancellationToken.None), "Reason is required");
    var first = await service.VoidAsync(1, "Test cleanup", CancellationToken.None);
    Check(first.Status == "Voided" && first.Amount == 85m && first.VoidReason == "Test cleanup" && first.VoidedDate.HasValue,
        "Void preserves original amount and records status, reason and date");
    var when = first.VoidedDate;
    var again = await service.VoidAsync(1, "Retry", CancellationToken.None);
    Check(again.VoidedDate == when && again.VoidReason == "Test cleanup", "Retry preserves the original cancellation");
    Check(await context.SalesInvoicePayments.IgnoreQueryFilters().CountAsync() == 5,
        "History retains all payment rows");
    Check(await context.SalesInvoicePayments.SumAsync(row => row.Amount) == 340m,
        "Financial queries exclude voided receipt");
    var bankStatus = await context.Database.SqlQueryRaw<string>("SELECT Status AS Value FROM banking.BankTransactions WHERE Id=1").SingleAsync();
    Check(bankStatus == "Cancelled", "Existing bank draft cancelled without replacement");
    var linkStatus = await context.Database.SqlQueryRaw<string>("SELECT State AS Value FROM banking.PaymentBankingLinks WHERE SourcePaymentId=1").SingleAsync();
    Check(linkStatus == "Cancelled", "Banking link cancelled to stop retries");
    var summaryService = new SalesInvoicePaymentService(new SalesInvoiceRepository(context),
        new SalesInvoicePaymentRepository(context), null!, null!);
    var summary = await summaryService.GetSummaryAsync(invoice.Id, CancellationToken.None);
    Check(summary!.PaidAmount == 340m && summary.BalanceDue == 660m && summary.Payments.Count == 5 &&
        summary.Payments.Any(row => row.Status == "Voided"), "Summary recalculates balance while retaining voided payment history");
    await Reject(() => service.VoidAsync(2, "Test", CancellationToken.None), "Posted bank transaction cannot be silently voided");
    await Reject(() => service.VoidAsync(3, "Test", CancellationToken.None), "Existing accounting posting requires reviewed reversal");
    Check(await context.SalesInvoicePayments.CountAsync() == 4, "Rejected voids leave payments recorded");
    await service.VoidAsync(4, "No bank link", CancellationToken.None);
    Check(await context.SalesInvoicePayments.CountAsync() == 3, "Unposted receipt without bank link can be voided");
    Console.WriteLine($"{checks} payment voiding checks passed.");
}
finally
{
    if (created) await context.Database.EnsureDeletedAsync();
}
