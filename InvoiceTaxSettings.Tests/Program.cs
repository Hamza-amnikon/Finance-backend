using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;

await PaymentModeCancellationChecks.RunAsync();

var database = "InvoiceTaxTests_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var context = new SalesInvoiceDbContext(new DbContextOptionsBuilder<SalesInvoiceDbContext>().UseSqlServer(connection).Options);
var created = false;
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + description);
    }

    Console.WriteLine("PASS: " + description);
    checks++;
}
async Task Reject(Func<Task> action, string description)
{
    try
    {
        await action();
    }
    catch (ArgumentException)
    {
        Check(true, description);
        return;
    }
    throw new Exception("FAIL: " + description);
}
try
{
    created = await context.Database.EnsureCreatedAsync();
    if (!created)
    {
        throw new Exception("Refusing to reuse an existing database.");
    }

    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA expenses");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA accounting");
    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE expenses.Companies(Id int PRIMARY KEY,Country nvarchar(2));
        INSERT INTO expenses.Companies VALUES(1,'AE'),(2,'IN');
        CREATE TABLE accounting.TaxTypes(Id int PRIMARY KEY,CountryCode nvarchar(2),Treatment nvarchar(40),IsActive bit);
        INSERT INTO accounting.TaxTypes VALUES(1,'AE','UaeVat',1),(2,'IN','IndiaCgstSgst',1),(3,'IN','IndiaIgst',1);
        CREATE TABLE accounting.TaxRates(Id int PRIMARY KEY,CompanyId int,TaxTypeId int,Code nvarchar(30),Name nvarchar(100),Rate decimal(9,4),ComponentsJson nvarchar(max),EffectiveFrom date,EffectiveTo date NULL,IsActive bit,RowVersion rowversion);
        INSERT INTO accounting.TaxRates(Id,CompanyId,TaxTypeId,Code,Name,Rate,ComponentsJson,EffectiveFrom,EffectiveTo,IsActive)
        VALUES(1,1,1,'VAT-TEST','Illustrative VAT',5,'[{{"Name":"VAT","Rate":5}}]','2026-11-01','2026-11-30',1),
        (2,2,2,'GST-TEST','Illustrative local',10,'[{{"Name":"CGST","Rate":5}},{{"Name":"SGST","Rate":5}}]','2026-11-01',NULL,1),
        (3,2,3,'IGST-TEST','Illustrative inter',5,'[{{"Name":"IGST","Rate":5}}]','2026-11-01',NULL,1);
        """);
    context.BranchMasters.AddRange(
        new BranchMaster { Name = "Dubai", CountryCode = "AE", AccountingCompanyId = 1, IsActive = true },
        new BranchMaster { Name = "Turbhe", CountryCode = "IN", AccountingCompanyId = 2, IsActive = true });
    await context.SaveChangesAsync();
    var service = new InvoiceTaxSettingsService(context);
    var date = new DateOnly(2026, 11, 9);
    var dubai = await service.ChoicesAsync("Dubai", date, default);
    var india = await service.ChoicesAsync("Turbhe", date, default);
    Check(dubai.Count == 1 && dubai[0].CompanyId == 1, "Dubai choices are restricted to UAE company settings");
    Check(india.Count == 2 && india.All(row => row.CompanyId == 2), "Turbhe choices are restricted to India company settings");
    Check((await service.ChoicesAsync("Dubai", new DateOnly(2026, 10, 31), default)).Count == 0,
        "A setting is unavailable before its effective start");
    Check((await service.ChoicesAsync("Dubai", new DateOnly(2026, 12, 1), default)).Count == 0,
        "A setting is unavailable after its effective end");
    Check((await service.ChoicesAsync("Dubai", new DateOnly(2026, 11, 30), default)).Count == 1,
        "The final effective date is inclusive");
    SalesInvoice Invoice(string branch, int count = 1)
    {
        return new SalesInvoice
        {
            Branch = branch,
            InvoiceDate = date,
            DueDate = date,
            Currency = "USD",
            ClientName = "Test",
            Items = Enumerable.Range(0, count).Select(_ => new SalesInvoiceItem
            {
                ServiceName = "Test service",
                Unit = ServiceUnit.FixedFee,
                Quantity = 1,
                UnitPrice = 100
            }).ToList()
        };
    }
    SalesInvoiceItemRequestDto Input(InvoiceTaxChoice choice)
    {
        return new SalesInvoiceItemRequestDto
        {
            TaxSettingId = choice.Id,
            TaxSettingRowVersion = Convert.ToBase64String(choice.RowVersion)
        };
    }
    var turbheInvoice = Invoice("Turbhe");
    turbheInvoice.Currency = "INR";
    turbheInvoice.Items.Single().UnitPrice = 1000;
    var localTax = india.Single(row => row.Treatment == "IndiaCgstSgst");
    await service.ApplyAsync(turbheInvoice, [Input(localTax)], default);
    new SalesInvoiceCalculator().Calculate(turbheInvoice, 2);
    Check(turbheInvoice.SubTotal == 1000 && turbheInvoice.TaxTotal == 100 && turbheInvoice.GrandTotal == 1100,
        "Turbhe 1000 subtotal with configured 10 percent tax totals 1100");
    Check(turbheInvoice.TaxTreatment == SalesInvoiceTaxTreatment.IndiaCgstSgst,
        "Turbhe local setting derives the CGST and SGST treatment");
    await Reject(() => service.ApplyAsync(Invoice("Turbhe"), [Input(dubai[0])], default),
        "A Turbhe invoice rejects a Dubai tax setting");
    var turbheNoTax = Invoice("Turbhe");
    turbheNoTax.Currency = "INR";
    turbheNoTax.Items.Single().UnitPrice = 1000;
    await service.ApplyAsync(turbheNoTax, [new SalesInvoiceItemRequestDto()], default);
    new SalesInvoiceCalculator().Calculate(turbheNoTax, 2);
    Check(turbheNoTax.SubTotal == 1000 && turbheNoTax.TaxTotal == 0 && turbheNoTax.GrandTotal == 1000,
        "Turbhe No tax preserves the 1000 subtotal and total with zero tax");

    var invoice = Invoice("Dubai");
    await service.ApplyAsync(invoice, [Input(dubai[0])], default);
    new SalesInvoiceCalculator().Calculate(invoice, 2);
    Check(invoice.TaxTotal == 5 && invoice.GrandTotal == 105 && invoice.Items.Single().TaxRate == 5,
        "Backend calculates tax from the selected saved setting");
    Check(invoice.TaxTreatment == SalesInvoiceTaxTreatment.UaeVat && invoice.UsesTaxSettings,
        "Treatment is derived from the selected setting");
    Check(invoice.Items.Single().TaxSettingSnapshotJson.Contains("VAT-TEST"),
        "The original tax configuration is frozen on the service line");
    await service.ValidateDraftAsync(invoice, default);
    Check(true, "An unchanged draft tax configuration validates before issue");
    await Reject(() => service.ApplyAsync(Invoice("Dubai"), [Input(india[0])], default),
        "A UAE invoice cannot use an India company tax setting");
    Check(typeof(SalesInvoiceItemRequestDto).GetProperty("TaxRate") == null,
        "The invoice request has no separate manual tax percentage");
    var stale = Input(dubai[0]);
    stale.TaxSettingRowVersion = "stale";
    await Reject(() => service.ApplyAsync(Invoice("Dubai"), [stale], default), "Stale lookup versions are rejected");
    await Reject(() => service.ApplyAsync(Invoice("Turbhe", 2), [Input(india[0]), Input(india[1])], default),
        "One invoice cannot mix incompatible India treatments");
    var untaxed = Invoice("Dubai");
    await service.ApplyAsync(untaxed, [new SalesInvoiceItemRequestDto()], default);
    Check(untaxed.TaxTreatment == SalesInvoiceTaxTreatment.NoTax && untaxed.Items.Single().TaxRate == 0,
        "Explicit no-tax service lines remain supported");
    context.SalesInvoices.Add(invoice);
    await context.SaveChangesAsync();
    var beforeSnapshot = invoice.Items.Single().TaxSettingSnapshotJson;
    var id = invoice.Id;
    await context.Database.ExecuteSqlRawAsync("UPDATE accounting.TaxRates SET Rate=6 WHERE Id=1");
    await Reject(() => service.ValidateDraftAsync(invoice, default), "A changed setting blocks issue until the draft is recreated");
    context.ChangeTracker.Clear();
    var saved = await context.SalesInvoices.AsNoTracking().Include(row => row.Items).SingleAsync(row => row.Id == id);
    Check(saved.Items.Single().TaxRate == 5 && saved.Items.Single().TaxSettingSnapshotJson == beforeSnapshot && saved.TaxTotal == 5,
        "Changing a master rate does not rewrite the saved invoice values");
    saved.Status = SalesInvoiceStatus.Issued;
    await service.ValidateDraftAsync(saved, default);
    Check(saved.TaxTotal == 5, "Issued documents do not reselect today's master tax configuration");
    var legacy = Invoice("Dubai");
    legacy.UsesTaxSettings = false;
    legacy.Items.Single().TaxRate = 8;
    await service.ValidateDraftAsync(legacy, default);
    Check(legacy.Items.Single().TaxRate == 8, "Legacy invoices retain their original percentage");
    await context.Database.ExecuteSqlRawAsync("UPDATE accounting.TaxRates SET IsActive=0 WHERE Id=1");
    Check((await service.ChoicesAsync("Dubai", date, default)).Count == 0, "Inactive configurations disappear from new invoice choices");
    Console.WriteLine($"All {checks} invoice-tax integration checks passed.");
}
finally
{
    if (created && database.StartsWith("InvoiceTaxTests_") && database.Length == 48)
    {
        await context.Database.EnsureDeletedAsync();
    }
}
