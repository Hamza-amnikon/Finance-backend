using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

namespace SalesInvoiceService.API.Services
{
    public class SalesInvoiceService : ISalesInvoiceService
    {
        private readonly ISalesInvoiceRepository _repository;
        private readonly IClientApiService _clientApiService;
        private readonly ICurrencyService _currencyService;
        private readonly ISalesInvoiceCalculator _calculator;
        private readonly IPaymentModeService _paymentModeService;

        public SalesInvoiceService(
            ISalesInvoiceRepository repository,
            IClientApiService clientApiService,
            ICurrencyService currencyService,
            ISalesInvoiceCalculator calculator,
            IPaymentModeService paymentModeService)
        {
            _repository = repository;
            _clientApiService = clientApiService;
            _currencyService = currencyService;
            _calculator = calculator;
            _paymentModeService = paymentModeService;
        }

        public async Task<SalesInvoiceResponseDto> CreateAsync(
            CreateSalesInvoiceDto dto,
            CancellationToken cancellationToken)
        {
            var client = await _clientApiService.GetByIdAsync(
                dto.ClientId,
                cancellationToken);

            if (client == null)
            {
                throw new ArgumentException("Client not found.");
            }

            if (!client.IsActive)
            {
                throw new ArgumentException(
                    "Select an active client.");
            }

            var currency = await _currencyService.GetByCodeAsync(
                dto.Currency,
                cancellationToken);

            if (currency == null)
            {
                throw new ArgumentException(
                    "Select an active currency.");
            }

            var preferredPaymentMode = string.Empty;

            if (!string.IsNullOrWhiteSpace(dto.PreferredPaymentMode))
            {
                var paymentMode = await _paymentModeService.GetByNameAsync(
                    dto.PreferredPaymentMode,
                    cancellationToken);

                if (paymentMode == null)
                {
                    throw new ArgumentException(
                        "Select an active payment mode.");
                }

                preferredPaymentMode = paymentMode.Name;
            }

            if (dto.Items == null ||
                dto.Items.Count == 0 ||
                dto.Items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Add at least one valid service line.");
            }

            var billingAddress = dto.BillingAddress == null
                ? BuildBillingAddress(client)
                : Clean(dto.BillingAddress);

            if (billingAddress.Length > 1000)
            {
                throw new ArgumentException(
                    "Billing address cannot exceed 1000 characters.");
            }

            var invoice = new SalesInvoice
            {
                ClientId = client.Id,
                ClientName = Clean(client.Name),
                CompanyName = Clean(client.CompanyName),
                BillingAddress = billingAddress,
                InvoiceDate = dto.InvoiceDate,
                DueDate = dto.DueDate,
                ServicePeriodStart = dto.ServicePeriodStart,
                ServicePeriodEnd = dto.ServicePeriodEnd,
                Currency = currency.CurrencyCode,
                Branch = Clean(dto.Branch),
                PaymentTerms = Clean(dto.PaymentTerms),
                PreferredPaymentMode = preferredPaymentMode,
                Reference = Clean(dto.Reference),
                Notes = Clean(dto.Notes),
                AdditionalTerms = Clean(dto.AdditionalTerms),
                Status = SalesInvoiceStatus.Draft,
                CreatedDate = DateTime.UtcNow,
                Items = dto.Items.Select(item =>
                    new SalesInvoiceItem
                    {
                        ServiceName = Clean(item.ServiceName),
                        Description = Clean(item.Description),
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate
                    }).ToList()
            };

            _calculator.Calculate(
                invoice,
                currency.DecimalPlaces);

            var created = await _repository.CreateAsync(
                invoice,
                cancellationToken);

            return ToResponse(created);
        }

        public async Task<SalesInvoiceResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var invoice = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            return invoice == null ? null : ToResponse(invoice);
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string BuildBillingAddress(
            InvoiceClientDto client)
        {
            var parts = new[]
            {
                client.Address,
                client.City,
                client.State,
                client.Country,
                client.PostalCode
            };

            return string.Join(
                ", ",
                parts
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()));
        }

        private static SalesInvoiceResponseDto ToResponse(
            SalesInvoice invoice)
        {
            return new SalesInvoiceResponseDto
            {
                Id = invoice.Id,
                ClientId = invoice.ClientId,
                ClientName = invoice.ClientName,
                CompanyName = invoice.CompanyName,
                BillingAddress = invoice.BillingAddress,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                ServicePeriodStart = invoice.ServicePeriodStart,
                ServicePeriodEnd = invoice.ServicePeriodEnd,
                Currency = invoice.Currency,
                Branch = invoice.Branch,
                PaymentTerms = invoice.PaymentTerms,
                PreferredPaymentMode = invoice.PreferredPaymentMode,
                Reference = invoice.Reference,
                SubTotal = invoice.SubTotal,
                DiscountTotal = invoice.DiscountTotal,
                TaxTotal = invoice.TaxTotal,
                GrandTotal = invoice.GrandTotal,
                Status = invoice.Status,
                Notes = invoice.Notes,
                AdditionalTerms = invoice.AdditionalTerms,
                CreatedDate = invoice.CreatedDate,
                ModifiedDate = invoice.ModifiedDate,
                IssuedDate = invoice.IssuedDate,
                VoidedDate = invoice.VoidedDate,
                VoidReason = invoice.VoidReason,
                RowVersion = invoice.RowVersion,
                Items = invoice.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new SalesInvoiceItemResponseDto
                    {
                        Id = item.Id,
                        LineNumber = item.LineNumber,
                        ServiceName = item.ServiceName,
                        Description = item.Description,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate,
                        LineSubTotal = item.LineSubTotal,
                        TaxAmount = item.TaxAmount,
                        LineTotal = item.LineTotal
                    }).ToList()
            };
        }
    }
}