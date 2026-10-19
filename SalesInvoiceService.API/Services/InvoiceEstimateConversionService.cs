using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public class InvoiceEstimateConversionService : IInvoiceEstimateConversionService
    {
        private readonly SalesInvoiceDbContext _context;
        private readonly IEstimateApiService _estimateApi;
        private readonly IClientApiService _clientApi;
        private readonly ICurrencyService _currencyService;
        private readonly ISalesInvoiceCalculator _calculator;
        private readonly IPaymentModeService _paymentModeService;

        public InvoiceEstimateConversionService(
            SalesInvoiceDbContext context,
            IEstimateApiService estimateApi,
            IClientApiService clientApi,
            ICurrencyService currencyService,
            ISalesInvoiceCalculator calculator,
            IPaymentModeService paymentModeService)
        {
            _context = context;
            _estimateApi = estimateApi;
            _clientApi = clientApi;
            _currencyService = currencyService;
            _calculator = calculator;
            _paymentModeService = paymentModeService;
        }

        public async Task<SalesInvoiceResponseDto> CreateAsync(
            CreateInvoiceFromEstimateDto dto,
            CancellationToken cancellationToken)
        {
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true))
                throw new ArgumentException(string.Join(" ", errors.Select(error => error.ErrorMessage)));

            var existing = await FindAsync(dto.EstimateId, cancellationToken);
            if (existing != null)
                return Replay(existing, dto);

            // No database lock is held during calls to other services.
            var source = await _estimateApi.GetByIdAsync(dto.EstimateId, cancellationToken);
            if (source == null)
                throw new ArgumentException("Estimate not found.");

            // Another request may have completed conversion during the lookup.
            if (source.Status != 2 || !source.RowVersion.SequenceEqual(dto.RowVersion))
            {
                existing = await FindAsync(dto.EstimateId, cancellationToken);
                if (existing != null)
                    return Replay(existing, dto);

                throw new InvalidOperationException("Only the current accepted estimate can be converted. Reload it first.");
            }

            if (source.Id != dto.EstimateId || source.ClientId == Guid.Empty ||
                string.IsNullOrWhiteSpace(source.EstimateNumber) || source.EstimateNumber.Length > 50 ||
                string.IsNullOrWhiteSpace(source.ClientName) || source.ClientName.Length > 200 ||
                source.CompanyName.Length > 200 || source.BillingAddress.Length > 1000 ||
                source.Notes.Length > 2000 || source.AdditionalTerms.Length > 2000 ||
                source.Items == null || source.Items.Count == 0 || source.Items.Any(item => item == null))
                throw new HttpRequestException("Estimate service returned incomplete estimate details.");

            var client = await _clientApi.GetByIdAsync(source.ClientId, cancellationToken);
            if (client == null || !client.IsActive || client.Id != source.ClientId)
                throw new ArgumentException("The estimate client must be active before conversion.");

            if (!string.Equals(client.Branch.Trim(), source.Branch, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(client.Currency.Trim(), source.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The client's branch or currency changed. Review the accepted estimate before conversion.");

            var currency = await _currencyService.GetByCodeAsync(source.Currency, cancellationToken);
            if (currency == null)
                throw new ArgumentException("The estimate currency must be active before conversion.");

            var preferredPaymentMode = string.Empty;
            if (!string.IsNullOrWhiteSpace(dto.PreferredPaymentMode))
            {
                var mode = await _paymentModeService.GetByNameAsync(dto.PreferredPaymentMode.Trim(), cancellationToken);
                if (mode == null)
                    throw new ArgumentException("Preferred payment mode must be active.");
                preferredPaymentMode = mode.Name;
            }

            var invoice = new SalesInvoice
            {
                ClientId = source.ClientId,
                ClientName = source.ClientName,
                CompanyName = source.CompanyName,
                BillingAddress = source.BillingAddress,
                SourceEstimateId = source.Id,
                SourceEstimateNumber = source.EstimateNumber,
                SourceEstimateRowVersion = dto.RowVersion.ToArray(),
                SourceEstimateInvoiceDate = dto.InvoiceDate,
                SourceEstimateDueDate = dto.DueDate,
                InvoiceDate = dto.InvoiceDate,
                DueDate = dto.DueDate,
                PreferredPaymentMode = preferredPaymentMode,
                ServicePeriodStart = source.ServicePeriodStart,
                ServicePeriodEnd = source.ServicePeriodEnd,
                Currency = currency.CurrencyCode,
                Branch = source.Branch,
                Notes = source.Notes,
                AdditionalTerms = source.AdditionalTerms,
                Items = source.Items.OrderBy(item => item.LineNumber).Select(item => new SalesInvoiceItem
                {
                    ServiceName = item.ServiceName,
                    Description = item.Description,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountEnabled = item.DiscountEnabled,
                    DiscountAmount = item.DiscountEnabled ? item.DiscountAmount : 0m,
                    TaxRate = item.TaxRate
                }).ToList()
            };

            _calculator.Calculate(invoice, currency.DecimalPlaces);

            if (invoice.SubTotal != source.SubTotal || invoice.DiscountTotal != source.DiscountTotal ||
                invoice.TaxTotal != source.TaxTotal || invoice.GrandTotal != source.GrandTotal ||
                !source.Items.OrderBy(item => item.LineNumber).Zip(invoice.Items, (saved, calculated) =>
                    saved.LineSubTotal == calculated.LineSubTotal && saved.TaxAmount == calculated.TaxAmount &&
                    saved.LineTotal == calculated.LineTotal && saved.DiscountAmount == calculated.DiscountAmount).All(match => match))
                throw new InvalidOperationException("Invoice totals do not match the accepted estimate. Review the estimate before conversion.");

            await using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            // The unique source index and range lock serialize concurrent conversions.
            existing = await _context.SalesInvoices
                .FromSqlInterpolated($"SELECT * FROM [sales].[SalesInvoices] WITH (UPDLOCK, HOLDLOCK) WHERE [SourceEstimateId] = {dto.EstimateId}")
                .AsNoTracking()
                .Include(record => record.Items)
                .SingleOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                var response = Replay(existing, dto);
                await transaction.CommitAsync(cancellationToken);
                return response;
            }

            _context.SalesInvoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return SalesInvoiceService.ToResponse(invoice);
        }

        private Task<SalesInvoice?> FindAsync(int estimateId, CancellationToken cancellationToken)
        {
            return _context.SalesInvoices.AsNoTracking().Include(invoice => invoice.Items)
                .SingleOrDefaultAsync(invoice => invoice.SourceEstimateId == estimateId, cancellationToken);
        }

        private static SalesInvoiceResponseDto Replay(SalesInvoice invoice, CreateInvoiceFromEstimateDto dto)
        {
            if (invoice.SourceEstimateInvoiceDate != dto.InvoiceDate || invoice.SourceEstimateDueDate != dto.DueDate ||
                !string.Equals(invoice.PreferredPaymentMode, dto.PreferredPaymentMode?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                invoice.SourceEstimateRowVersion == null || !invoice.SourceEstimateRowVersion.SequenceEqual(dto.RowVersion))
                throw new InvalidOperationException("This estimate already has an invoice from a different conversion request. Open the existing invoice.");

            return SalesInvoiceService.ToResponse(invoice);
        }
    }
}
