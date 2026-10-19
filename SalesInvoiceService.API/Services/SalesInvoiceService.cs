using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
        private readonly IBranchService _branchService;

        public SalesInvoiceService(
            ISalesInvoiceRepository repository,
            IClientApiService clientApiService,
            ICurrencyService currencyService,
            ISalesInvoiceCalculator calculator,
            IPaymentModeService paymentModeService,
            IBranchService branchService)
        {
            _repository = repository;
            _clientApiService = clientApiService;
            _currencyService = currencyService;
            _calculator = calculator;
            _paymentModeService = paymentModeService;
            _branchService = branchService;
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

            var branch = Clean(client.Branch);

            if (branch.Length == 0 || branch.Length > 20)
            {
                throw new ArgumentException(
                    "Update the client's managing branch before creating an invoice.");
            }

            if (!string.Equals(
                Clean(dto.Branch),
                branch,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The managing branch must match the selected client. Reload the invoice form and select the client again.");
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
                Branch = branch,
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
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountAmount = item.DiscountEnabled
                            ? item.DiscountAmount
                            : 0m,
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

        public async Task<SalesInvoiceResponseDto?> IssueAsync(
            int id,
            IssueSalesInvoiceDto dto,
            CancellationToken cancellationToken)
        {
            var invoice = await _repository.GetByIdAsync(id, cancellationToken);

            if (invoice == null)
            {
                return null;
            }

            // A retry returns the previously issued invoice without a new number.
            if (invoice.Status == SalesInvoiceStatus.Issued)
            {
                return ToResponse(invoice);
            }

            if (invoice.Status != SalesInvoiceStatus.Draft)
            {
                throw new InvalidOperationException("Only draft invoices can be issued.");
            }

            if (!invoice.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            var client = await _clientApiService.GetByIdAsync(invoice.ClientId, cancellationToken);

            if (client == null || !client.IsActive)
            {
                throw new ArgumentException("The invoice client must be active before issuing.");
            }

            if (!string.Equals(Clean(client.Branch), invoice.Branch, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The client's branch changed. Review the draft before issuing.");
            }

            var branch = await _branchService.GetByNameAsync(invoice.Branch, cancellationToken);

            if (branch == null || string.IsNullOrWhiteSpace(branch.CompanyName) ||
                string.IsNullOrWhiteSpace(branch.Address) || branch.CountryCode.Length != 2)
            {
                throw new ArgumentException("Complete the branch company name, address and country code in Organization settings before issuing.");
            }

            var currency = await _currencyService.GetByCodeAsync(invoice.Currency, cancellationToken);

            if (currency == null)
            {
                throw new ArgumentException("The invoice currency must be active before issuing.");
            }

            if (invoice.InvoiceDate == default || invoice.DueDate < invoice.InvoiceDate)
            {
                throw new ArgumentException("Review the invoice date and due date before issuing.");
            }

            var originalTotals = (invoice.SubTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.GrandTotal);
            _calculator.Calculate(invoice, currency.DecimalPlaces);

            if (originalTotals != (invoice.SubTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.GrandTotal))
            {
                throw new InvalidOperationException("Draft totals no longer match the service lines. Review the draft before issuing.");
            }

            var issuer = new BranchInvoiceProfileFieldsDto
            {
                CompanyName = branch.CompanyName,
                Address = branch.Address,
                CountryCode = branch.CountryCode,
                CompanyRegistrationNumber = branch.CompanyRegistrationNumber,
                TaxRegistrationNumber = branch.TaxRegistrationNumber,
                BankAccountName = branch.BankAccountName,
                BankName = branch.BankName,
                BankAccountNumber = branch.BankAccountNumber,
                BankIban = branch.BankIban,
                BankSwiftCode = branch.BankSwiftCode,
                Phone = branch.Phone,
                Email = branch.Email,
                InvoiceNotes = branch.InvoiceNotes,
            };

            invoice.IssuerSnapshotJson = JsonSerializer.Serialize(issuer);
            invoice.InvoiceTemplateVersion = "amnikon-v1";
            invoice.Status = SalesInvoiceStatus.Issued;
            invoice.IssuedDate = DateTime.UtcNow;
            invoice.ModifiedDate = invoice.IssuedDate;

            var issued = await _repository.IssueAsync(invoice, dto.RowVersion, cancellationToken);

            return ToResponse(issued);
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

        public async Task<(List<SalesInvoiceResponseDto> Data, int Total)>
            GetAllAsync(
                int page,
                int pageSize,
                CancellationToken cancellationToken)
        {
            var result = await _repository.GetAllAsync(
                page,
                pageSize,
                cancellationToken);

            return (
                result.Data.Select(ToResponse).ToList(),
                result.Total);
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

        internal static SalesInvoiceResponseDto ToResponse(
            SalesInvoice invoice)
        {
            return new SalesInvoiceResponseDto
            {
                Id = invoice.Id,
                ClientId = invoice.ClientId,
                SourceEstimateId = invoice.SourceEstimateId,
                SourceEstimateNumber = invoice.SourceEstimateNumber,
                SourceEstimateRowVersion = invoice.SourceEstimateRowVersion,
                SourceEstimateInvoiceDate = invoice.SourceEstimateInvoiceDate,
                SourceEstimateDueDate = invoice.SourceEstimateDueDate,
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
                Issuer = string.IsNullOrWhiteSpace(invoice.IssuerSnapshotJson)
                    ? null
                    : JsonSerializer.Deserialize<BranchInvoiceProfileFieldsDto>(invoice.IssuerSnapshotJson),
                InvoiceTemplateVersion = invoice.InvoiceTemplateVersion,
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
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountAmount = item.DiscountEnabled
                            ? item.DiscountAmount
                            : 0m,
                        TaxRate = item.TaxRate,
                        LineSubTotal = item.LineSubTotal,
                        TaxAmount = item.TaxAmount,
                        LineTotal = item.LineTotal
                    }).ToList()
            };
        }
    }
}