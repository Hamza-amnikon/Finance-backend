using System.Data;
using Microsoft.EntityFrameworkCore;
using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;

namespace BillService.API.Services
{
    public class BillConversionService : IBillConversionService
    {
        private readonly BillTaxSettingsService _taxSettings;
        private readonly BillDbContext _context;
        private readonly IBillService _billService;
        private readonly IBillCalculator _calculator;
        public BillConversionService(
            BillDbContext db,
            IBillService bills,
            IBillCalculator calculator,
            BillTaxSettingsService taxSettings
        )
        {
            _taxSettings = taxSettings;
            _context = db;
            _billService = bills;
            _calculator = calculator;
        }

        public async Task<BillResponseDto?> ConvertAsync(
            int purchaseOrderId,
            byte[] rowVersion,
            CancellationToken cancellationToken
        )
        {
            if (purchaseOrderId < 1 || rowVersion == null || rowVersion.Length != 8)
            {
                throw new ArgumentException("A valid purchase order ID and row version are required.");
            }

            await using var transaction = _context.Database.CurrentTransaction == null ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var source = await _context.Database.SqlQuery<PurchaseOrderSnapshot>($"""
                SELECT [Id],[Status],[RowVersion],[PurchaseOrderNumber],[VendorId],[VendorName],[CompanyName],
                    [BillingAddress],[ShippingAddress],[Branch],[Currency],[CurrencyDecimalPlaces],[PaymentTerms],
                    [Reference],[Notes],[AdditionalTerms],[GrandTotal],[UsesTaxSettings],[TaxTreatment]
                FROM [purchases].[PurchaseOrders] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {purchaseOrderId}
                """).SingleOrDefaultAsync(cancellationToken);
            if (source == null)
            {
                return null;
            }

            var existing = await _context.Bills.AsNoTracking().Where(row => row.SourcePurchaseOrderId == purchaseOrderId).Select(row => (int?)row.Id).SingleOrDefaultAsync(cancellationToken);
            if (existing.HasValue)
            {
                var result = await _billService.GetByIdAsync(existing.Value, cancellationToken);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            }

            if (source.Status != "Issued")
            {
                throw new InvalidOperationException("Only issued purchase orders can be converted to bills.");
            }

            if (!source.RowVersion.SequenceEqual(rowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            var lines = await _context.Database.SqlQuery<PurchaseOrderItemSnapshot>($"""
                SELECT [LineNumber],[ItemName],[Description],[Unit],[Quantity],[UnitPrice],[DiscountEnabled],[DiscountAmount],[DiscountType],[DiscountValue],[TaxRate],[TaxSettingId],[TaxSettingSnapshotJson]
                FROM [purchases].[PurchaseOrderItems] WHERE [PurchaseOrderId] = {purchaseOrderId}
                """).OrderBy(row => row.LineNumber).ToListAsync(cancellationToken);
            var bill = new Bill
            {
                RequestId = Guid.NewGuid(),
                RequestFingerprint = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                SourcePurchaseOrderId = source.Id,
                SourcePurchaseOrderNumber = source.PurchaseOrderNumber,
                VendorId = source.VendorId,
                VendorName = source.VendorName,
                CompanyName = source.CompanyName,
                BillingAddress = source.BillingAddress,
                ShippingAddress = source.ShippingAddress,
                Branch = source.Branch,
                Currency = source.Currency,
                UsesTaxSettings = source.UsesTaxSettings,
                TaxTreatment = source.UsesTaxSettings ? Enum.Parse<BillTaxTreatment>(source.TaxTreatment) : null,
                CurrencyDecimalPlaces = source.CurrencyDecimalPlaces,
                PaymentTerms = source.PaymentTerms,
                Reference = source.Reference,
                Notes = source.Notes,
                AdditionalTerms = source.AdditionalTerms,
                BillDate = DateOnly.FromDateTime(DateTime.Today),
                Items = lines.Select(line => new BillItem
                {
                    ItemName = line.ItemName,
                    Description = line.Description,
                    Unit = Enum.Parse<BillUnit>(line.Unit),
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountEnabled = line.DiscountEnabled,
                    DiscountType = line.DiscountType,
                    DiscountValue = line.DiscountValue,
                    DiscountAmount = line.DiscountAmount,
                    TaxRate = line.TaxRate,
                    TaxSettingId = line.TaxSettingId,
                    TaxSettingSnapshotJson = line.TaxSettingSnapshotJson
                }).ToList()
            };
            try
            {
                await _taxSettings.ValidateDraftAsync(bill, cancellationToken);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    "The purchase order's saved tax setting cannot be used for the bill date. " +
                    "Review its company, effective dates and current version. Create a direct bill " +
                    "with a valid setting or issue a revised order. The source order is unchanged.",
                    exception);
            }
            _calculator.Calculate(bill, bill.CurrencyDecimalPlaces);
            if (bill.GrandTotal != source.GrandTotal)
            {
                throw new InvalidOperationException("The purchase order totals could not be reproduced. Review the purchase order before converting.");
            }

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync(cancellationToken);
            var response = await _billService.GetByIdAsync(bill.Id, cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return response;
        }
    }
}
