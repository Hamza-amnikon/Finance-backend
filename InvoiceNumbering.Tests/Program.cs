using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

var databaseName = "InvoiceNumberingChecks_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<SalesInvoiceDbContext>()
    .UseSqlServer(connection)
    .Options;

await using var setup = new SalesInvoiceDbContext(options);
var created = false;

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }

    Console.WriteLine("PASS: " + message);
}

async Task<SalesInvoice> Issue(string branchName, DateOnly date)
{
    await using var context = new SalesInvoiceDbContext(options);
    var repository = new SalesInvoiceRepository(context);
    var invoice = new SalesInvoice
    {
        ClientId = Guid.NewGuid(),
        ClientName = "Numbering test",
        Branch = branchName,
        Currency = "USD",
        InvoiceDate = date,
        DueDate = date.AddDays(7),
        Status = SalesInvoiceStatus.Draft
    };

    await repository.CreateAsync(invoice, CancellationToken.None);
    var version = invoice.RowVersion.ToArray();
    context.ChangeTracker.Clear();
    invoice.Status = SalesInvoiceStatus.Issued;

    return await repository.IssueAsync(invoice, version, CancellationToken.None);
}

try
{
    created = await setup.Database.EnsureCreatedAsync();

    if (!created)
    {
        throw new InvalidOperationException("Refusing to reuse an existing test database.");
    }

    setup.BranchMasters.AddRange(
        new BranchMaster { Name = "Office Alpha", InvoiceNumberPrefix = "01" },
        new BranchMaster { Name = "Office Beta", InvoiceNumberPrefix = "02" },
        new BranchMaster { Name = "Office Other", InvoiceNumberPrefix = "HQ9" },
        new BranchMaster { Name = "Office Legacy" });
    await setup.SaveChangesAsync();

    var date = new DateOnly(2026, 11, 12);
    var first = await Issue("Office Alpha", date);
    var second = await Issue("Office Beta", date);
    var third = await Issue("Office Alpha", date);
    var other = await Issue("Office Other", date);
    var legacy = await Issue("Office Legacy", date);

    Check(first.InvoiceNumber == "01-2026/011/001", "Prefix comes from the saved branch, independent of branch name.");
    Check(second.InvoiceNumber == "02-2026/011/002", "The second branch uses its own prefix and preserves the existing monthly sequence.");
    Check(third.InvoiceNumber == "01-2026/011/003", "Repeated issuance of new drafts advances the sequence without duplicate numbers.");
    Check(other.InvoiceNumber == "HQ9-2026/011/004", "Future branch prefixes do not require a source-code mapping.");
    Check(legacy.InvoiceNumber == "2026/011/005", "An unconfigured branch retains the existing number format.");

    var branch = await setup.BranchMasters.SingleAsync(entry => entry.Name == "Office Alpha");
    branch.InvoiceNumberPrefix = "A7";
    await setup.SaveChangesAsync();

    var changed = await Issue("Office Alpha", date);
    var nextMonth = await Issue("Office Beta", date.AddMonths(1));

    Check(changed.InvoiceNumber == "A7-2026/011/006", "A changed saved prefix applies to future invoices.");
    Check(nextMonth.InvoiceNumber == "02-2026/012/001", "The existing monthly reset is preserved.");
    Check(await setup.SalesInvoices.AsNoTracking().Where(entry => entry.Id == first.Id)
        .Select(entry => entry.InvoiceNumber).SingleAsync() == first.InvoiceNumber,
        "Previously issued numbers remain unchanged after branch settings change.");

    await using var conflictContext = new SalesInvoiceDbContext(options);
    var stale = await conflictContext.SalesInvoices.AsNoTracking().SingleAsync(entry => entry.Id == third.Id);
    var staleRepository = new SalesInvoiceRepository(conflictContext);

    try
    {
        await staleRepository.IssueAsync(stale, new byte[8], CancellationToken.None);
        throw new InvalidOperationException("Stale version was accepted.");
    }
    catch (DbUpdateConcurrencyException)
    {
        Check(true, "A stale version cannot overwrite a saved invoice number.");
    }

    await using var verification = new SalesInvoiceDbContext(options);
    var counter = await verification.Set<InvoiceNumberCounter>().SingleAsync(entry => entry.PeriodKey == "202611");
    Check(counter.LastNumber == 6, "Failed issuance rolls back the counter increment.");
}
finally
{
    if (created)
    {
        await setup.Database.EnsureDeletedAsync();
    }
}
