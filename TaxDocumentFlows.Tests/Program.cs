using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.Data;
using SalesInvoiceService.API.Data;
using PurchaseOrderService.API.Data;
using BillService.API.Data;
using E = SalesEstimateService.API.Models;
using ED = SalesEstimateService.API.DTOs;
using ES = SalesEstimateService.API.Services;
using I = SalesInvoiceService.API.Models;
using ID = SalesInvoiceService.API.DTOs;
using IS = SalesInvoiceService.API.Services;
using P = PurchaseOrderService.API.Models;
using PD = PurchaseOrderService.API.DTOs;
using PS = PurchaseOrderService.API.Services;
using B = BillService.API.Models;
using BD = BillService.API.DTOs;
using BS = BillService.API.Services;

var database = "TaxFlowTests_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var invoices = new SalesInvoiceDbContext(new DbContextOptionsBuilder<SalesInvoiceDbContext>().UseSqlServer(connection).Options);
await using var estimates = new SalesEstimateDbContext(new DbContextOptionsBuilder<SalesEstimateDbContext>().UseSqlServer(connection).Options);
await using var orders = new PurchaseOrderDbContext(new DbContextOptionsBuilder<PurchaseOrderDbContext>().UseSqlServer(connection).Options);
await using var bills = new BillDbContext(new DbContextOptionsBuilder<BillDbContext>().UseSqlServer(connection).Options);
var created = false;
var checks = DiscountCalculationChecks.Run();
void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + message);
    }
    Console.WriteLine("PASS: " + message);
    checks++;
}
async Task Reject(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (ArgumentException)
    {
        Check(true, message);
        return;
    }
    throw new Exception("FAIL: " + message);
}
try
{
    created = await invoices.Database.EnsureCreatedAsync();
    if (!created)
    {
        throw new Exception("Refusing to reuse an existing database.");
    }
    foreach (var context in new DbContext[] { estimates, orders, bills })
    {
        foreach (var batch in Regex.Split(context.Database.GenerateCreateScript(), @"^GO\s*$", RegexOptions.Multiline))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await context.Database.ExecuteSqlRawAsync(batch);
            }
        }
    }
    await invoices.Database.ExecuteSqlRawAsync("CREATE SCHEMA expenses");
    await invoices.Database.ExecuteSqlRawAsync("CREATE SCHEMA accounting");
    await invoices.Database.ExecuteSqlRawAsync("""
        CREATE TABLE expenses.Companies(Id int PRIMARY KEY,Country nvarchar(2));
        INSERT INTO expenses.Companies VALUES(1,'AE'),(2,'IN');
        CREATE TABLE accounting.TaxTypes(Id int PRIMARY KEY,CountryCode nvarchar(2),Treatment nvarchar(40),IsActive bit);
        INSERT INTO accounting.TaxTypes VALUES(1,'AE','UaeVat',1),(2,'IN','IndiaCgstSgst',1),(3,'IN','IndiaIgst',1);
        CREATE TABLE accounting.TaxRates(Id int PRIMARY KEY,CompanyId int,TaxTypeId int,Code nvarchar(30),Name nvarchar(100),Rate decimal(9,4),ComponentsJson nvarchar(max),EffectiveFrom date,EffectiveTo date NULL,IsActive bit,RowVersion rowversion);
        INSERT INTO accounting.TaxRates(Id,CompanyId,TaxTypeId,Code,Name,Rate,ComponentsJson,EffectiveFrom,EffectiveTo,IsActive)
        VALUES(1,1,1,'VAT-TEST','Illustrative VAT',5,'[{{"Name":"VAT","Rate":5}}]','2020-01-01','2099-12-31',1),
        (2,2,2,'GST-TEST','Illustrative local',10,'[{{"Name":"CGST","Rate":5}},{{"Name":"SGST","Rate":5}}]','2020-01-01','2099-12-31',1),
        (3,2,3,'IGST-TEST','Illustrative inter',5,'[{{"Name":"IGST","Rate":5}}]','2020-01-01','2099-12-31',1);
        CREATE TABLE purchases.VendorMaster(Id uniqueidentifier PRIMARY KEY,Name nvarchar(200),CompanyName nvarchar(200),
            Address nvarchar(200),City nvarchar(100),State nvarchar(100),Country nvarchar(100),PostalCode nvarchar(20),
            Branch nvarchar(20),Currency nvarchar(3),PaymentTerms nvarchar(200),IsActive bit);
        INSERT INTO purchases.VendorMaster VALUES('11111111-1111-1111-1111-111111111111','Test vendor','','','','','','','Dubai','USD','',1);
        """);
    invoices.BranchMasters.AddRange(
        new I.BranchMaster { Name = "Dubai", CountryCode = "AE", AccountingCompanyId = 1, IsActive = true },
        new I.BranchMaster { Name = "Turbhe", CountryCode = "IN", AccountingCompanyId = 2, IsActive = true });
    invoices.CurrencyMasters.Add(new I.CurrencyMaster { CurrencyCode = "USD", CurrencyName = "Test", DecimalPlaces = 2, IsActive = true });
    await invoices.SaveChangesAsync();
    var date = new DateOnly(2026, 11, 10);
    var client = new ClientStub();
    var currency = new CurrencyStub();
    var estimateTax = new ES.SalesEstimateTaxSettingsService(estimates);
    var estimateService = new ES.SalesEstimateService(new SalesEstimateService.API.Repositories.SalesEstimateRepository(estimates),
        client, currency, new ES.SalesEstimateCalculator(), estimateTax);
    var invoiceTax = new IS.InvoiceTaxSettingsService(invoices);
    var converter = new IS.InvoiceEstimateConversionService(invoices, new EstimateStub(estimateService), client,
        currency, new IS.SalesInvoiceCalculator(), new PaymentModeStub(), invoiceTax);
    var choices = await estimateTax.ChoicesAsync("Dubai", date, default);
    var india = await estimateTax.ChoicesAsync("Turbhe", date, default);
    Check(choices.Count == 1 && india.Count == 2, "Estimate choices follow company and country");
    Check((await estimateTax.ChoicesAsync("Dubai", new DateOnly(2019, 1, 1), default)).Count == 0,
        "Estimate date excludes future rates");
    ED.CreateSalesEstimateDto EstimateRequest(bool taxed = true)
    {
        return new ED.CreateSalesEstimateDto
        {
            ClientId = client.Id,
            Branch = "Dubai",
            Currency = "USD",
            EstimateDate = date,
            ValidUntil = date.AddDays(30),
            Items = [new ED.SalesEstimateItemRequestDto
            {
                ServiceName = "Test service", Unit = E.ServiceUnit.FixedFee, Quantity = 1, UnitPrice = 1000,
                TaxSettingId = taxed ? choices[0].Id : null,
                TaxSettingRowVersion = taxed ? Convert.ToBase64String(choices[0].RowVersion) : ""
            }]
        };
    }
    var estimateRequest = EstimateRequest();
    var estimatePreview = await estimateService.PreviewAsync(estimateRequest, default);
    Check(estimatePreview.TaxTotal == 50 && estimatePreview.GrandTotal == 1050, "Estimate preview uses the saved percentage");
    var wrongCountry = EstimateRequest();
    wrongCountry.Items[0].TaxSettingId = india[0].Id;
    wrongCountry.Items[0].TaxSettingRowVersion = Convert.ToBase64String(india[0].RowVersion);
    await Reject(() => estimateService.PreviewAsync(wrongCountry, default), "Estimate rejects a different company's tax setting");
    Check((await estimateService.PreviewAsync(EstimateRequest(false), default)).TaxTotal == 0,
        "Estimate No tax produces zero tax");
    var stale = EstimateRequest();
    stale.Items[0].TaxSettingRowVersion = "stale";
    await Reject(() => estimateService.PreviewAsync(stale, default), "Estimate rejects stale tax selections");
    var estimate = await estimateService.CreateAsync(estimateRequest, default);
    Check(estimate.UsesTaxSettings && estimate.Items[0].TaxSettingId == 1, "Estimate save returns its selected setting");
    var sent = (await estimateService.MarkAsSentAsync(estimate.Id, new ED.SalesEstimateActionDto { RowVersion = estimate.RowVersion }, default))!;
    var accepted = (await estimateService.AcceptAsync(sent.Id, new ED.SalesEstimateActionDto { RowVersion = sent.RowVersion }, default))!;
    Check(accepted.Status == E.SalesEstimateStatus.Accepted, "Configured estimate can be sent and accepted");
    var conversion = new ID.CreateInvoiceFromEstimateDto { EstimateId = accepted.Id, RowVersion = accepted.RowVersion, InvoiceDate = date, DueDate = date.AddDays(1) };
    var invoice = await converter.CreateAsync(conversion, default);
    Check(invoice.UsesTaxSettings && invoice.TaxTreatment == I.SalesInvoiceTaxTreatment.UaeVat && invoice.GrandTotal == 1050,
        "Estimate conversion carries the tax treatment and amount to the invoice");
    Check(invoice.Items[0].TaxSettingSnapshotJson == estimate.Items[0].TaxSettingSnapshotJson,
        "Estimate conversion preserves the original selected tax snapshot");
    Check((await converter.CreateAsync(conversion, default)).Id == invoice.Id, "Repeating estimate conversion returns the same invoice");
    var noTaxEstimate = await estimateService.CreateAsync(EstimateRequest(false), default);
    sent = (await estimateService.MarkAsSentAsync(noTaxEstimate.Id, new ED.SalesEstimateActionDto { RowVersion = noTaxEstimate.RowVersion }, default))!;
    accepted = (await estimateService.AcceptAsync(sent.Id, new ED.SalesEstimateActionDto { RowVersion = sent.RowVersion }, default))!;
    var noTaxInvoice = await converter.CreateAsync(new ID.CreateInvoiceFromEstimateDto { EstimateId = accepted.Id, RowVersion = accepted.RowVersion, InvoiceDate = date, DueDate = date }, default);
    Check(noTaxInvoice.TaxTotal == 0 && noTaxInvoice.GrandTotal == 1000, "No-tax estimate converts without adding tax");

    var orderTax = new PS.PurchaseOrderTaxSettingsService(orders);
    var orderService = new PS.PurchaseOrderService(orders, new PurchaseOrderService.API.Repositories.PurchaseOrderRepository(orders),
        new PS.PurchaseOrderCalculator(), orderTax);
    var orderChoices = await orderTax.ChoicesAsync("Dubai", date, default);
    var orderIndia = await orderTax.ChoicesAsync("Turbhe", date, default);
    Check(orderChoices.Count == 1 && orderIndia.Count == 2, "Purchase order choices follow company and country");
    PD.CreatePurchaseOrderDto OrderRequest(bool taxed = true)
    {
        return new PD.CreatePurchaseOrderDto
        {
            RequestId = Guid.NewGuid(),
            VendorId = client.Id,
            Branch = "Dubai",
            Currency = "USD",
            OrderDate = date,
            Items = [new PD.PurchaseOrderItemRequestDto
            {
                ItemName = "Test item", Unit = P.PurchaseUnit.Piece, Quantity = 1, UnitPrice = 1000,
                TaxSettingId = taxed ? orderChoices[0].Id : null,
                TaxSettingRowVersion = taxed ? Convert.ToBase64String(orderChoices[0].RowVersion) : ""
            }]
        };
    }
    var orderRequest = OrderRequest();
    var orderPreview = await orderService.PreviewAsync(orderRequest, default);
    Check(orderPreview.TaxTotal == 50 && orderPreview.GrandTotal == 1050, "Purchase order preview uses the saved percentage");
    var foreignOrder = OrderRequest();
    foreignOrder.Items[0].TaxSettingId = orderIndia[0].Id;
    foreignOrder.Items[0].TaxSettingRowVersion = Convert.ToBase64String(orderIndia[0].RowVersion);
    await Reject(() => orderService.PreviewAsync(foreignOrder, default), "Purchase order rejects another company tax setting");
    var order = await orderService.CreateAsync(orderRequest, default);
    orders.ChangeTracker.Clear();
    var issuedOrder = (await orderService.ChangeStatusAsync(order.Id, P.PurchaseOrderStatus.Issued, order.RowVersion, default))!;
    Check(issuedOrder.UsesTaxSettings && issuedOrder.TaxTreatment == P.PurchaseOrderTaxTreatment.UaeVat,
        "Purchase order can be issued with its saved treatment");
    var billTax = new BS.BillTaxSettingsService(bills);
    var billService = new BS.BillService(bills, new BillService.API.Repositories.BillRepository(bills), new BS.BillCalculator(), null!, null!, billTax);
    var billConverter = new BS.BillConversionService(bills, billService, new BS.BillCalculator(), billTax);
    var bill = (await billConverter.ConvertAsync(order.Id, issuedOrder.RowVersion, default))!;
    Check(bill.UsesTaxSettings && bill.TaxTreatment == B.BillTaxTreatment.UaeVat && bill.GrandTotal == 1050,
        "Purchase order conversion carries its selected treatment and total into the bill");
    Check(bill.Items[0].TaxSettingSnapshotJson == order.Items[0].TaxSettingSnapshotJson,
        "Purchase order conversion preserves its tax setting snapshot");
    Check((await billConverter.ConvertAsync(order.Id, issuedOrder.RowVersion, default))?.Id == bill.Id,
        "Repeating purchase order conversion returns the same bill");
    var untaxedOrder = await orderService.CreateAsync(OrderRequest(false), default);
    orders.ChangeTracker.Clear();
    var untaxedIssued = (await orderService.ChangeStatusAsync(untaxedOrder.Id, P.PurchaseOrderStatus.Issued, untaxedOrder.RowVersion, default))!;
    var untaxedBill = (await billConverter.ConvertAsync(untaxedOrder.Id, untaxedIssued.RowVersion, default))!;
    Check(untaxedBill.TaxTotal == 0 && untaxedBill.GrandTotal == 1000, "No-tax purchase order converts without adding tax");
    var indiaOrderRequest = OrderRequest();
    indiaOrderRequest.Branch = "Turbhe";
    var local = orderIndia.Single(row => row.Treatment == "IndiaCgstSgst");
    indiaOrderRequest.Items[0].TaxSettingId = local.Id;
    indiaOrderRequest.Items[0].TaxSettingRowVersion = Convert.ToBase64String(local.RowVersion);
    var indiaOrder = await orderService.CreateAsync(indiaOrderRequest, default);
    orders.ChangeTracker.Clear();
    var indiaIssued = (await orderService.ChangeStatusAsync(indiaOrder.Id, P.PurchaseOrderStatus.Issued, indiaOrder.RowVersion, default))!;
    var indiaBill = (await billConverter.ConvertAsync(indiaOrder.Id, indiaIssued.RowVersion, default))!;
    Check(indiaBill.TaxTreatment == B.BillTaxTreatment.IndiaCgstSgst && indiaBill.TaxTotal == 100 && indiaBill.GrandTotal == 1100,
        "Turbhe CGST and SGST carry through purchase order conversion");
    client.Branch = "Turbhe";
    var indiaEstimateRequest = EstimateRequest();
    indiaEstimateRequest.Branch = "Turbhe";
    var interstate = india.Single(row => row.Treatment == "IndiaIgst");
    indiaEstimateRequest.Items[0].TaxSettingId = interstate.Id;
    indiaEstimateRequest.Items[0].TaxSettingRowVersion = Convert.ToBase64String(interstate.RowVersion);
    var indiaEstimate = await estimateService.CreateAsync(indiaEstimateRequest, default);
    sent = (await estimateService.MarkAsSentAsync(indiaEstimate.Id, new ED.SalesEstimateActionDto { RowVersion = indiaEstimate.RowVersion }, default))!;
    accepted = (await estimateService.AcceptAsync(sent.Id, new ED.SalesEstimateActionDto { RowVersion = sent.RowVersion }, default))!;
    var indiaInvoice = await converter.CreateAsync(new ID.CreateInvoiceFromEstimateDto
    {
        EstimateId = accepted.Id,
        RowVersion = accepted.RowVersion,
        InvoiceDate = date,
        DueDate = date
    }, default);
    Check(indiaInvoice.TaxTreatment == I.SalesInvoiceTaxTreatment.IndiaIgst && indiaInvoice.GrandTotal == 1050,
        "Turbhe IGST carries through estimate conversion without a manual treatment");
    client.Branch = "Dubai";

    var expiredEstimate = await estimateService.CreateAsync(EstimateRequest(), default);
    sent = (await estimateService.MarkAsSentAsync(expiredEstimate.Id, new ED.SalesEstimateActionDto { RowVersion = expiredEstimate.RowVersion }, default))!;
    accepted = (await estimateService.AcceptAsync(sent.Id, new ED.SalesEstimateActionDto { RowVersion = sent.RowVersion }, default))!;
    await Reject(() => converter.CreateAsync(new ID.CreateInvoiceFromEstimateDto
    {
        EstimateId = accepted.Id,
        RowVersion = accepted.RowVersion,
        InvoiceDate = new DateOnly(2100, 1, 1),
        DueDate = new DateOnly(2100, 1, 2)
    }, default), "Conversion rejects a saved setting outside the destination invoice date");

    var legacyEstimate = new E.SalesEstimate
    {
        ClientId = client.Id,
        ClientName = "Legacy client",
        Branch = "Dubai",
        Currency = "USD",
        EstimateDate = date,
        ValidUntil = date.AddDays(30),
        UsesTaxSettings = false,
        Items = [new E.SalesEstimateItem { ServiceName = "Legacy service", Quantity = 1, UnitPrice = 100, TaxRate = 8,
            DiscountEnabled = true, DiscountAmount = 10 }]
    };
    new ES.SalesEstimateCalculator().Calculate(legacyEstimate, 2);
    estimates.SalesEstimates.Add(legacyEstimate);
    await estimates.SaveChangesAsync();
    sent = (await estimateService.MarkAsSentAsync(legacyEstimate.Id, new ED.SalesEstimateActionDto { RowVersion = legacyEstimate.RowVersion }, default))!;
    accepted = (await estimateService.AcceptAsync(sent.Id, new ED.SalesEstimateActionDto { RowVersion = sent.RowVersion }, default))!;
    var legacyInvoice = await converter.CreateAsync(new ID.CreateInvoiceFromEstimateDto
    {
        EstimateId = accepted.Id,
        RowVersion = accepted.RowVersion,
        InvoiceDate = date,
        DueDate = date,
        TaxTreatment = I.SalesInvoiceTaxTreatment.UaeVat
    }, default);
    Check(!legacyInvoice.UsesTaxSettings && legacyInvoice.GrandTotal == 97.20m && legacyInvoice.Items[0].TaxRate == 8 &&
        legacyInvoice.Items[0].DiscountType == "Fixed" && legacyInvoice.Items[0].DiscountValue == null && legacyInvoice.DiscountTotal == 10,
        "Legacy estimate send and conversion retain the recorded rate and amounts");
    var legacyOrder = new P.PurchaseOrder
    {
        RequestId = Guid.NewGuid(),
        VendorId = client.Id,
        VendorName = "Legacy vendor",
        Branch = "Dubai",
        Currency = "USD",
        CurrencyDecimalPlaces = 2,
        OrderDate = date,
        UsesTaxSettings = false,
        Items = [new P.PurchaseOrderItem { ItemName = "Legacy item", Quantity = 1, UnitPrice = 100, TaxRate = 8,
            DiscountEnabled = true, DiscountAmount = 10 }]
    };
    new PS.PurchaseOrderCalculator().Calculate(legacyOrder, 2);
    orders.PurchaseOrders.Add(legacyOrder);
    await orders.SaveChangesAsync();
    var legacyIssued = (await orderService.ChangeStatusAsync(legacyOrder.Id, P.PurchaseOrderStatus.Issued, legacyOrder.RowVersion, default))!;
    var legacyBill = (await billConverter.ConvertAsync(legacyOrder.Id, legacyIssued.RowVersion, default))!;
    Check(!legacyBill.UsesTaxSettings && legacyBill.GrandTotal == 97.20m && legacyBill.Items[0].TaxRate == 8 &&
        legacyBill.Items[0].DiscountType == "Fixed" && legacyBill.Items[0].DiscountValue == null && legacyBill.DiscountTotal == 10,
        "Legacy purchase order conversion retains its recorded tax and amounts");
    var changedOrder = await orderService.CreateAsync(OrderRequest(), default);
    orders.ChangeTracker.Clear();
    var changedIssued = (await orderService.ChangeStatusAsync(changedOrder.Id, P.PurchaseOrderStatus.Issued, changedOrder.RowVersion, default))!;
    var discountedEstimateRequest = EstimateRequest();
    discountedEstimateRequest.Items[0].DiscountEnabled = true;
    discountedEstimateRequest.Items[0].DiscountType = "Percentage";
    discountedEstimateRequest.Items[0].DiscountValue = 10;
    var discountedEstimate = await estimateService.CreateAsync(discountedEstimateRequest, default);
    Check(discountedEstimate.DiscountTotal == 100 && discountedEstimate.GrandTotal == 945,
        "Percentage estimate saves a monetary discount calculated by the backend");
    discountedEstimateRequest.Items[0].DiscountValue = 20;
    var discountedEdit = (await estimateService.UpdateAsync(discountedEstimate.Id, new ED.UpdateSalesEstimateDto
    {
        ClientId = client.Id,
        Branch = "Dubai",
        Currency = "USD",
        EstimateDate = date,
        ValidUntil = date.AddDays(30),
        RowVersion = discountedEstimate.RowVersion,
        Items = discountedEstimateRequest.Items
    }, default))!;
    Check(discountedEdit.Items[0].DiscountValue == 20 && discountedEdit.DiscountTotal == 200 && discountedEdit.GrandTotal == 840,
        "Editing an estimate preserves the entered percentage and recalculates its deduction");
    sent = (await estimateService.MarkAsSentAsync(discountedEdit.Id, new ED.SalesEstimateActionDto { RowVersion = discountedEdit.RowVersion }, default))!;
    accepted = (await estimateService.AcceptAsync(sent.Id, new ED.SalesEstimateActionDto { RowVersion = sent.RowVersion }, default))!;
    var discountedInvoice = await converter.CreateAsync(new ID.CreateInvoiceFromEstimateDto
    {
        EstimateId = accepted.Id,
        RowVersion = accepted.RowVersion,
        InvoiceDate = date,
        DueDate = date
    }, default);
    Check(discountedInvoice.Items[0].DiscountType == "Percentage" && discountedInvoice.Items[0].DiscountValue == 20 &&
        discountedInvoice.DiscountTotal == 200 && discountedInvoice.TaxTotal == 40 && discountedInvoice.GrandTotal == 840,
        "Estimate conversion preserves percentage metadata and calculated invoice amounts");
    var discountedOrderRequest = OrderRequest();
    discountedOrderRequest.Items[0].DiscountEnabled = true;
    discountedOrderRequest.Items[0].DiscountType = "Percentage";
    discountedOrderRequest.Items[0].DiscountValue = 10;
    var discountedOrder = await orderService.CreateAsync(discountedOrderRequest, default);
    discountedOrderRequest.Items[0].Quantity = 2;
    orders.ChangeTracker.Clear();
    var discountedOrderEdit = (await orderService.UpdateAsync(discountedOrder.Id, new PD.UpdatePurchaseOrderDto
    {
        VendorId = client.Id,
        Branch = "Dubai",
        Currency = "USD",
        OrderDate = date,
        RowVersion = discountedOrder.RowVersion,
        Items = discountedOrderRequest.Items
    }, default))!;
    Check(discountedOrderEdit.Items[0].DiscountValue == 10 && discountedOrderEdit.DiscountTotal == 200 && discountedOrderEdit.GrandTotal == 1890,
        "Changing order quantity recalculates the percentage deduction");
    orders.ChangeTracker.Clear();
    var discountedIssued = (await orderService.ChangeStatusAsync(discountedOrderEdit.Id, P.PurchaseOrderStatus.Issued, discountedOrderEdit.RowVersion, default))!;
    var discountedBill = (await billConverter.ConvertAsync(discountedOrderEdit.Id, discountedIssued.RowVersion, default))!;
    Check(discountedBill.Items[0].DiscountType == "Percentage" && discountedBill.Items[0].DiscountValue == 10 &&
        discountedBill.DiscountTotal == 200 && discountedBill.TaxTotal == 90 && discountedBill.GrandTotal == 1890,
        "Purchase order conversion preserves percentage metadata and calculated bill amounts");
    bills.ChangeTracker.Clear();
    var discountedBillEdit = (await billService.UpdateAsync(discountedBill.Id, new BD.UpdateBillDto
    {
        VendorId = client.Id,
        Branch = "Dubai",
        Currency = "USD",
        BillDate = discountedBill.BillDate,
        RowVersion = discountedBill.RowVersion,
        Items = [new BD.BillItemRequestDto
        {
            ItemName = "Test item", AccountCode = "TEST-EXPENSE", UnitPrice = 1000, Quantity = 3,
            DiscountEnabled = true, DiscountType = "Percentage", DiscountValue = 10,
            TaxSettingId = choices[0].Id, TaxSettingRowVersion = Convert.ToBase64String(choices[0].RowVersion)
        }]
    }, default))!;
    Check(discountedBillEdit.DiscountTotal == 300 && discountedBillEdit.GrandTotal == 2835 && discountedBillEdit.Items[0].DiscountValue == 10,
        "Editing a converted bill uses its entered percentage instead of its previous monetary discount");
    var changedEstimate = await estimateService.CreateAsync(EstimateRequest(), default);
    await estimates.Database.ExecuteSqlRawAsync("UPDATE accounting.TaxRates SET Name='Changed setting' WHERE Id=1");
    await Reject(() => estimateService.MarkAsSentAsync(changedEstimate.Id, new ED.SalesEstimateActionDto { RowVersion = changedEstimate.RowVersion }, default),
        "A changed master blocks sending a stale estimate draft");
    await Reject(() => billConverter.ConvertAsync(changedOrder.Id, changedIssued.RowVersion, default),
        "Purchase order conversion rejects a setting changed after issue");
    Check((await billConverter.ConvertAsync(order.Id, issuedOrder.RowVersion, default))?.Id == bill.Id,
        "A retry recovers an existing conversion even after a master setting changes");
    var stored = await estimateService.GetByIdAsync(estimate.Id, default);
    Check(stored!.GrandTotal == 1050 && stored.Items[0].TaxSettingSnapshotJson == estimate.Items[0].TaxSettingSnapshotJson,
        "Changing tax settings does not rewrite accepted estimates");
    Check((await billService.GetByIdAsync(bill.Id, default))!.GrandTotal == 1050,
        "Changing tax settings does not rewrite converted bills");
    Console.WriteLine($"All {checks} tax document flow checks passed.");
}
finally
{
    if (created && database.StartsWith("TaxFlowTests_") && database.Length == "TaxFlowTests_".Length + 32)
    {
        await invoices.Database.EnsureDeletedAsync();
    }
}

sealed class ClientStub : ES.IClientApiService, IS.IClientApiService
{
    public string Branch { get; set; } = "Dubai";
    public Guid Id { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    Task<ED.EstimateClientDto?> ES.IClientApiService.GetByIdAsync(Guid id, CancellationToken token)
    {
        return Task.FromResult<ED.EstimateClientDto?>(new ED.EstimateClientDto { Id = id, Name = "Test client", Branch = Branch, Currency = "USD", IsActive = true });
    }
    Task<ID.InvoiceClientDto?> IS.IClientApiService.GetByIdAsync(Guid id, CancellationToken token)
    {
        return Task.FromResult<ID.InvoiceClientDto?>(new ID.InvoiceClientDto { Id = id, Name = "Test client", Branch = Branch, Currency = "USD", IsActive = true });
    }
}
sealed class CurrencyStub : ES.ICurrencyApiService, IS.ICurrencyService
{
    Task<ED.EstimateCurrencyDto?> ES.ICurrencyApiService.GetByCodeAsync(string code, CancellationToken token)
    {
        return Task.FromResult<ED.EstimateCurrencyDto?>(new ED.EstimateCurrencyDto { CurrencyCode = code, DecimalPlaces = 2, IsActive = true });
    }
    public Task<I.CurrencyMaster?> GetByCodeAsync(string code, CancellationToken token)
    {
        return Task.FromResult<I.CurrencyMaster?>(new I.CurrencyMaster { CurrencyCode = code, CurrencyName = "Test", DecimalPlaces = 2, IsActive = true });
    }
    public Task<List<I.CurrencyMaster>> GetActiveAsync(CancellationToken token)
    {
        throw new NotSupportedException();
    }
}
sealed class EstimateStub : IS.IEstimateApiService
{
    private readonly ES.ISalesEstimateService _service;

    public EstimateStub(ES.ISalesEstimateService service)
    {
        _service = service;
    }
    public async Task<ID.EstimateForInvoiceDto?> GetByIdAsync(int id, CancellationToken token)
    {
        var source = await _service.GetByIdAsync(id, token);
        return JsonSerializer.Deserialize<ID.EstimateForInvoiceDto>(JsonSerializer.Serialize(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
sealed class PaymentModeStub : IS.IPaymentModeService
{
    public Task<List<I.PaymentMode>> GetActiveAsync(CancellationToken token)
    {
        throw new NotSupportedException();
    }
    public Task<I.PaymentMode?> GetByNameAsync(string name, CancellationToken token)
    {
        throw new NotSupportedException();
    }
}
