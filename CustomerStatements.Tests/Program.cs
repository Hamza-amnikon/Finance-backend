using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Controllers;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;

var database = "CustomerStatementChecks_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var context = new SalesInvoiceDbContext(
    new DbContextOptionsBuilder<SalesInvoiceDbContext>()
        .UseSqlServer(connection)
        .Options);
var created = false;
var clientId = Guid.NewGuid();
var from = new DateOnly(2026, 11, 12);
var to = new DateOnly(2026, 11, 20);
var checks = 0;

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }

    checks++;
    Console.WriteLine("PASS: " + message);
}

SalesInvoice Invoice(string number, DateOnly date, decimal total)
{
    return new SalesInvoice
    {
        ClientId = clientId,
        ClientName = "Test customer",
        InvoiceNumber = number,
        InvoiceDate = date,
        DueDate = date.AddDays(7),
        Branch = "Test Office",
        Currency = "USD",
        GrandTotal = total,
        Status = SalesInvoiceStatus.Issued
    };
}

SalesInvoicePayment Payment(SalesInvoice invoice, DateOnly date, decimal amount)
{
    return new SalesInvoicePayment
    {
        SalesInvoice = invoice,
        RequestId = Guid.NewGuid(),
        PaymentDate = date,
        Currency = invoice.Currency,
        PaymentMode = "Cash",
        Amount = amount
    };
}

SalesCreditNote Credit(SalesInvoice invoice, DateOnly date, decimal amount)
{
    return new SalesCreditNote
    {
        SalesInvoice = invoice,
        RequestId = Guid.NewGuid(),
        CreditNoteDate = date,
        Currency = invoice.Currency,
        Reason = "Test credit",
        GrandTotal = amount,
        Status = SalesCreditNoteStatus.Issued
    };
}

try
{
    created = await context.Database.EnsureCreatedAsync();

    if (!created)
    {
        throw new InvalidOperationException("Refusing to reuse an existing database.");
    }

    var older = Invoice("OLD", from.AddMonths(-1), 1000);
    var current = Invoice("CURRENT", from, 200);
    var future = Invoice("FUTURE", to.AddDays(10), 300);
    var differentCurrency = Invoice("AED", from, 500);
    differentCurrency.Currency = "AED";
    var differentBranch = Invoice("OTHER-OFFICE", from, 600);
    differentBranch.Branch = "Another Office";
    var differentClient = Invoice("OTHER-CUSTOMER", from, 700);
    differentClient.ClientId = Guid.NewGuid();
    var draft = Invoice("DRAFT", from, 900);
    draft.Status = SalesInvoiceStatus.Draft;
    context.SalesInvoices.AddRange(older, current, future, differentCurrency,
        differentBranch, differentClient, draft);

    var voided = Payment(older, from, 50);
    voided.Status = "Voided";
    context.SalesInvoicePayments.AddRange(
        Payment(older, from.AddDays(-1), 100),
        Payment(older, to, 150),
        Payment(current, to.AddDays(1), 20),
        voided);
    context.SalesCreditNotes.AddRange(
        Credit(older, from.AddDays(-2), 100),
        Credit(older, from.AddDays(1), 20),
        Credit(current, to.AddDays(1), 10));
    context.CustomerCreditApplications.Add(new CustomerCreditApplication
    {
        RequestId = Guid.NewGuid(),
        SourceInvoice = older,
        TargetInvoice = current,
        ClientId = clientId,
        Branch = "Test Office",
        Currency = "USD",
        Amount = 20,
        AppliedAtUtc = new DateTime(2026, 11, 20, 23, 59, 59, DateTimeKind.Utc)
    });
    await context.SaveChangesAsync();
    context.ChangeTracker.Clear();

    var service = new CustomerStatementService(context);
    var period = await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None, from, to);
    Check(period.OpeningBalance == 800, "Opening includes earlier invoices, credits and payments.");
    Check(period.InvoiceTotal == 200 && period.CreditNoteTotal == 20 && period.CashPaymentTotal == 150,
        "Period totals exclude earlier and later movements, drafts, voided payments and other scopes.");
    Check(period.NetBalance == 830 && period.Lines.Last().RunningBalance == 830,
        "Running and closing balances reconcile with the opening balance.");
    Check(period.Lines.Any(line => line.Date == from && line.Type == "Invoice") &&
        period.Lines.Any(line => line.Date == to && line.Type == "CashPayment"),
        "Both boundary dates are inclusive.");
    Check(period.Lines.Any(line => line.InvoiceNumber == "OLD" && line.Type == "CashPayment"),
        "In-period payments for an older invoice remain included.");
    Check(period.Lines.Single(line => line.Type == "CreditApplication").BalanceChange == 0,
        "A credit transfer on the UTC end date does not double-count the customer balance.");

    var empty = await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None,
        from.AddMonths(1), to.AddMonths(1));
    Check(empty.Lines.Count == 0 && empty.OpeningBalance == 1100 && empty.NetBalance == 1100,
        "A period without movements retains its outstanding opening and closing balances.");
    var all = await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None);
    Check(all.OpeningBalance == 0 && all.InvoiceTotal == 1500 && all.CreditNoteTotal == 130 &&
        all.CashPaymentTotal == 270 && all.NetBalance == 1100,
        "All dates preserves the full-history totals.");
    var through = await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None, null, to);
    Check(through.OpeningBalance == 0 && through.NetBalance == 830,
        "A To-only statement includes history through the cutoff.");
    var since = await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None, from);
    Check(since.OpeningBalance == 800 && since.NetBalance == 1100,
        "A From-only statement retains the opening and includes later history.");
    var before = await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None,
        from.AddYears(-1), to.AddYears(-1));
    Check(before.Lines.Count == 0 && before.NetBalance == 0,
        "A period before the first transaction has zero balance.");
    var missing = await service.GetAsync(Guid.NewGuid(), "Test Office", "USD", CancellationToken.None, from, to);
    Check(missing.FromDate == from && missing.ToDate == to && missing.NetBalance == 0,
        "An empty customer result retains the requested date range.");

    var controller = new CustomerStatementsController(service);
    Check(await controller.Get(clientId, "Test Office", "USD", to, from, CancellationToken.None)
        is BadRequestObjectResult, "Reversed dates return HTTP 400.");

    Check((await service.GetAsync(clientId, "Test Office", "USD", CancellationToken.None, from, to))
        .NetBalance == period.NetBalance && await context.SalesInvoices.CountAsync() == 7,
        "Refreshing the statement does not create or change financial records.");
    Console.WriteLine($"{checks} customer statement checks passed.");
}
finally
{
    if (created)
    {
        await context.Database.EnsureDeletedAsync();
    }
}
