using Microsoft.EntityFrameworkCore;
using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;
using BillService.API.Services;

var database = "BillTaxTests_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var context = new BillDbContext(new DbContextOptionsBuilder<BillDbContext>().UseSqlServer(connection).Options);
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
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA sales");
    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE sales.BranchMasters(Id int PRIMARY KEY,Name nvarchar(20),CountryCode nvarchar(2),AccountingCompanyId int NULL,IsActive bit);
        INSERT INTO sales.BranchMasters VALUES(1,'Dubai','AE',1,1),(2,'Turbhe','IN',2,1);
        """);
    var service = new BillTaxSettingsService(context);
    var date = new DateOnly(2026, 11, 10);
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
    Bill Bill(string branch, int count = 1)
    {
        return new Bill
        {
            RequestId = Guid.NewGuid(),
            Branch = branch,
            BillDate = date,
            DueDate = date,
            Currency = "USD",
            VendorName = "Test",
            Items = Enumerable.Range(0, count).Select(_ => new BillItem
            {
                ItemName = "Test service",
                AccountCode = "TEST-EXPENSE",
                Unit = BillUnit.FixedFee,
                Quantity = 1,
                UnitPrice = 100
            }).ToList()
        };
    }
    BillItemRequestDto Input(BillTaxChoice choice)
    {
        return new BillItemRequestDto
        {
            TaxSettingId = choice.Id,
            TaxSettingRowVersion = Convert.ToBase64String(choice.RowVersion)
        };
    }
    var turbheBill = Bill("Turbhe");
    turbheBill.Currency = "INR";
    turbheBill.Items.Single().UnitPrice = 1000;
    var localTax = india.Single(row => row.Treatment == "IndiaCgstSgst");
    await service.ApplyAsync(turbheBill, [Input(localTax)], default);
    new BillCalculator().Calculate(turbheBill, 2);
    Check(turbheBill.SubTotal == 1000 && turbheBill.TaxTotal == 100 && turbheBill.GrandTotal == 1100,
        "Turbhe 1000 subtotal with configured 10 percent tax totals 1100");
    Check(turbheBill.TaxTreatment == BillTaxTreatment.IndiaCgstSgst,
        "Turbhe local setting derives the CGST and SGST treatment");
    await Reject(() => service.ApplyAsync(Bill("Turbhe"), [Input(dubai[0])], default),
        "A Turbhe bill rejects a Dubai tax setting");
    var turbheNoTax = Bill("Turbhe");
    turbheNoTax.Currency = "INR";
    turbheNoTax.Items.Single().UnitPrice = 1000;
    await service.ApplyAsync(turbheNoTax, [new BillItemRequestDto()], default);
    new BillCalculator().Calculate(turbheNoTax, 2);
    Check(turbheNoTax.SubTotal == 1000 && turbheNoTax.TaxTotal == 0 && turbheNoTax.GrandTotal == 1000,
        "Turbhe No tax preserves the 1000 subtotal and total with zero tax");

    var bill = Bill("Dubai");
    await service.ApplyAsync(bill, [Input(dubai[0])], default);
    new BillCalculator().Calculate(bill, 2);
    Check(bill.TaxTotal == 5 && bill.GrandTotal == 105 && bill.Items.Single().TaxRate == 5,
        "Backend calculates tax from the selected saved setting");
    Check(bill.TaxTreatment == BillTaxTreatment.UaeVat && bill.UsesTaxSettings,
        "Treatment is derived from the selected setting");
    Check(bill.Items.Single().TaxSettingSnapshotJson.Contains("VAT-TEST"),
        "The original tax configuration is frozen on the service line");
    await service.ValidateDraftAsync(bill, default);
    Check(true, "An unchanged draft tax configuration validates before issue");
    await Reject(() => service.ApplyAsync(Bill("Dubai"), [Input(india[0])], default),
        "A UAE bill cannot use an India company tax setting");
    Check(typeof(BillItemRequestDto).GetProperty("TaxRate") == null,
        "The bill request has no separate manual tax percentage");
    var stale = Input(dubai[0]);
    stale.TaxSettingRowVersion = "stale";
    await Reject(() => service.ApplyAsync(Bill("Dubai"), [stale], default), "Stale lookup versions are rejected");
    await Reject(() => service.ApplyAsync(Bill("Turbhe", 2), [Input(india[0]), Input(india[1])], default),
        "One bill cannot mix incompatible India treatments");
    var untaxed = Bill("Dubai");
    await service.ApplyAsync(untaxed, [new BillItemRequestDto()], default);
    Check(untaxed.TaxTreatment == BillTaxTreatment.NoTax && untaxed.Items.Single().TaxRate == 0,
        "Explicit no-tax service lines remain supported");
    context.Bills.Add(bill);
    await context.SaveChangesAsync();
    var beforeSnapshot = bill.Items.Single().TaxSettingSnapshotJson;
    var id = bill.Id;
    await context.Database.ExecuteSqlRawAsync("UPDATE accounting.TaxRates SET Rate=6 WHERE Id=1");
    await Reject(() => service.ValidateDraftAsync(bill, default), "A changed setting blocks issue until the draft is recreated");
    context.ChangeTracker.Clear();
    var saved = await context.Bills.AsNoTracking().Include(row => row.Items).SingleAsync(row => row.Id == id);
    Check(saved.Items.Single().TaxRate == 5 && saved.Items.Single().TaxSettingSnapshotJson == beforeSnapshot && saved.TaxTotal == 5,
        "Changing a master rate does not rewrite the saved bill values");
    saved.Status = BillStatus.Issued;
    await service.ValidateDraftAsync(saved, default);
    Check(saved.TaxTotal == 5, "Issued documents do not reselect today's master tax configuration");
    var legacy = Bill("Dubai");
    legacy.UsesTaxSettings = false;
    legacy.Items.Single().TaxRate = 8;
    await service.ValidateDraftAsync(legacy, default);
    Check(legacy.Items.Single().TaxRate == 8, "Legacy bills retain their original percentage");
    await context.Database.ExecuteSqlRawAsync("UPDATE accounting.TaxRates SET IsActive=0 WHERE Id=1");
    Check((await service.ChoicesAsync("Dubai", date, default)).Count == 0, "Inactive configurations disappear from new bill choices");
    await context.Database.ExecuteSqlRawAsync("UPDATE accounting.TaxRates SET Rate=5,IsActive=1 WHERE Id=1");
    var repository = new BillService.API.Repositories.BillRepository(context);
    var oldDraft = Bill("Dubai");
    oldDraft.Items.Single().TaxRate = 8;
    new BillCalculator().Calculate(oldDraft, 2);
    await repository.CreateAsync(oldDraft, default);
    var oldDraftId = oldDraft.Id;
    var oldVersion = oldDraft.RowVersion.ToArray();
    context.ChangeTracker.Clear();
    var replacement = Bill("Dubai");
    replacement.Id = oldDraftId;
    var currentChoice = (await service.ChoicesAsync("Dubai", date, default)).Single();
    await service.ApplyAsync(replacement, [Input(currentChoice)], default);
    new BillCalculator().Calculate(replacement, 2);
    await repository.UpdateDraftAsync(replacement, oldVersion, default);
    context.ChangeTracker.Clear();
    var updated = (await repository.GetByIdAsync(oldDraftId, default))!;
    Check(updated.UsesTaxSettings && updated.Items.Single().TaxSettingId == currentChoice.Id && updated.TaxTotal == 5,
        "Saving an edited legacy draft persists the selected setting and compatibility flag");
    await service.ValidateDraftAsync(updated, default);
    Check(true, "The saved bill snapshot validates after a database reload");
    await service.ApplyAsync(updated, [new BillItemRequestDto()], default);
    new BillCalculator().Calculate(updated, 2);
    Check(updated.Items.Single().TaxSettingId == null && updated.Items.Single().TaxSettingSnapshotJson == "" && updated.TaxTotal == 0,
        "Selecting No tax clears the old setting reference and snapshot");
    checks += await BillFlowChecks.RunAsync(context);
    Console.WriteLine($"All {checks} bill-tax integration checks passed.");
}
finally
{
    if (created && database.StartsWith("BillTaxTests_") && database.Length == "BillTaxTests_".Length + 32)
    {
        await context.Database.EnsureDeletedAsync();
    }
}
