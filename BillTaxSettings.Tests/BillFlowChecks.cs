using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;
using BillService.API.Repositories;
using BillService.API.Services;
using Microsoft.EntityFrameworkCore;

internal static class BillFlowChecks
{
    public static async Task<int> RunAsync(BillDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE purchases.VendorMaster(
                Id uniqueidentifier PRIMARY KEY, Name nvarchar(200), CompanyName nvarchar(200),
                Address nvarchar(200),City nvarchar(100),State nvarchar(100),Country nvarchar(100),
                PostalCode nvarchar(20),Branch nvarchar(20),Currency nvarchar(3),PaymentTerms nvarchar(200),IsActive bit);
            INSERT INTO purchases.VendorMaster VALUES('11111111-1111-1111-1111-111111111111',
                'Test vendor','','','','','','','Dubai','USD','',1);
            CREATE TABLE sales.CurrencyMasters(CurrencyCode nvarchar(3),CurrencyName nvarchar(100),DecimalPlaces int,IsActive bit);
            INSERT INTO sales.CurrencyMasters VALUES('USD','Test currency',2,1);
            """);
        var tax = new BillTaxSettingsService(context);
        var accounting = new AccountingStub();
        var service = new BillService.API.Services.BillService(context, new BillRepository(context),
            new BillCalculator(), accounting, new ExchangeStub(), tax);
        var choice = (await tax.ChoicesAsync("Dubai", new DateOnly(2026, 11, 10), default)).Single();
        var request = new CreateBillDto
        {
            RequestId = Guid.NewGuid(),
            VendorId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            VendorInvoiceNumber = "TEST-FLOW-001",
            Branch = "Dubai",
            Currency = "USD",
            BillDate = new DateOnly(2026, 11, 10),
            DueDate = new DateOnly(2026, 11, 11),
            TaxTreatment = BillTaxTreatment.IndiaIgst,
            Items = [new BillItemRequestDto
            {
                ItemName = "Test service",
                AccountCode = "TEST-EXPENSE",
                UnitPrice = 100,
                Quantity = 1,
                TaxSettingId = choice.Id,
                TaxSettingRowVersion = Convert.ToBase64String(choice.RowVersion)
            }]
        };
        var preview = await service.PreviewAsync(request, default);
        Check(preview.GrandTotal == 105 && preview.TaxTreatment == BillTaxTreatment.UaeVat,
            "Bill preview derives the rate and treatment from the saved company setting");
        var created = await service.CreateAsync(request, default);
        Check(created.UsesTaxSettings && created.Items.Single().TaxSettingId == choice.Id && created.GrandTotal == 105,
            "Creating a bill persists and returns its selected tax setting");
        var repeated = await service.CreateAsync(request, default);
        Check(repeated.Id == created.Id, "Retrying the same create request does not duplicate the bill");
        context.ChangeTracker.Clear();
        request.Items.Single().Quantity = 2;
        var updated = (await service.UpdateAsync(created.Id, new UpdateBillDto
        {
            RowVersion = created.RowVersion,
            VendorId = request.VendorId,
            VendorInvoiceNumber = request.VendorInvoiceNumber,
            Branch = request.Branch,
            Currency = request.Currency,
            BillDate = request.BillDate,
            DueDate = request.DueDate,
            Items = request.Items
        }, default))!;
        Check(updated.GrandTotal == 210 && updated.TaxTotal == 10 && updated.UsesTaxSettings,
            "Draft edits recalculate using the selected saved setting");
        context.ChangeTracker.Clear();
        var issued = await service.ChangeStatusAsync(updated.Id, BillStatus.Issued, updated.RowVersion, default);
        Check(issued?.Status == BillStatus.Issued && accounting.Posting?.Taxes.Single().SystemKey == "INPUT_VAT" &&
            accounting.Posting.Taxes.Single().Amount == 10 && accounting.Posting.TotalAmount == 210,
            "Issuing the bill sends the saved tax amount to the existing accounting posting flow");
        request.RequestId = Guid.NewGuid();
        request.VendorInvoiceNumber = "";
        request.DueDate = null;
        var popupDraft = await service.CreateAsync(request, default);
        context.ChangeTracker.Clear();
        try
        {
            await service.ChangeStatusAsync(popupDraft.Id, BillStatus.Issued, popupDraft.RowVersion, default,
                new BillActionDto { VendorInvoiceNumber = "POPUP-TEST", DueDate = request.BillDate.AddDays(-1) });
            throw new Exception("FAIL: Invalid issue date was accepted.");
        }
        catch (ArgumentException)
        {
            context.ChangeTracker.Clear();
        }
        var unchanged = await service.GetByIdAsync(popupDraft.Id, default);
        Check(unchanged!.Status == BillStatus.Draft && unchanged.VendorInvoiceNumber == "" && unchanged.DueDate == null,
            "Invalid issue details leave the saved draft unchanged");
        var popupIssued = (await service.ChangeStatusAsync(popupDraft.Id, BillStatus.Issued, popupDraft.RowVersion, default,
            new BillActionDto { VendorInvoiceNumber = "  POPUP-TEST  ", DueDate = request.BillDate.AddDays(3) }))!;
        Check(popupIssued.Status == BillStatus.Issued && popupIssued.VendorInvoiceNumber == "POPUP-TEST" &&
            popupIssued.DueDate == request.BillDate.AddDays(3),
            "Issue confirmation saves vendor invoice number and due date with the issue action");
        context.ChangeTracker.Clear();
        var persisted = (await service.GetByIdAsync(popupDraft.Id, default))!;
        Check(persisted.VendorInvoiceNumber == "POPUP-TEST" && persisted.DueDate == request.BillDate.AddDays(3) &&
            persisted.GrandTotal == popupDraft.GrandTotal,
            "Reloading the bill returns popup details without changing totals");
        var retry = (await service.ChangeStatusAsync(popupDraft.Id, BillStatus.Issued, popupDraft.RowVersion, default,
            new BillActionDto { VendorInvoiceNumber = "Must not overwrite", DueDate = request.BillDate.AddDays(10) }))!;
        Check(retry.Id == popupIssued.Id && retry.BillNumber == popupIssued.BillNumber &&
            retry.VendorInvoiceNumber == "POPUP-TEST" && retry.DueDate == popupIssued.DueDate,
            "Retrying issue keeps the same bill and frozen details");
        return 9;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception("FAIL: " + message);
        }
        Console.WriteLine("PASS: " + message);
    }

    private sealed class ExchangeStub : IBillExchangeRateService
    {
        public Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken token)
        {
            return Task.FromResult(1m);
        }
    }

    private sealed class AccountingStub : IBillAccountingApiService
    {
        public BillAccountingPostingDto? Posting { get; private set; }

        public Task<BillAccountingCompanyDto?> GetCompanyAsync(int companyId, CancellationToken token)
        {
            return Task.FromResult<BillAccountingCompanyDto?>(new BillAccountingCompanyDto
            {
                Id = companyId, Country = "AE", Currency = "USD"
            });
        }

        public Task<BillAccountingResultDto> ReverseBillAsync(int billId, CancellationToken token)
        {
            return Task.FromResult(new BillAccountingResultDto { JournalEntryId = 2, Status = "Reversed" });
        }

        public Task<BillAccountingResultDto> PostBillAsync(BillAccountingPostingDto posting, CancellationToken token)
        {
            Posting = posting;
            return Task.FromResult(new BillAccountingResultDto { JournalEntryId = 1, Status = "Posted" });
        }
    }
}
