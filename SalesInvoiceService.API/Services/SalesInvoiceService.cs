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
        private readonly InvoiceTaxSettingsService _taxSettings;
        private readonly IClientApiService _clientApiService;
        private readonly ICurrencyService _currencyService;
        private readonly ISalesInvoiceCalculator _calculator;
        private readonly IPaymentModeService _paymentModeService;
        private readonly IBranchService _branchService;
        private readonly IAccountingApiService _accountingApiService;
        private readonly IInvoiceExchangeRateService
            _exchangeRateService;

        public SalesInvoiceService(
            ISalesInvoiceRepository repository,
            IClientApiService clientApiService,
            ICurrencyService currencyService,
            ISalesInvoiceCalculator calculator,
            IPaymentModeService paymentModeService,
            IBranchService branchService,
            IAccountingApiService accountingApiService,
            IInvoiceExchangeRateService exchangeRateService,
            InvoiceTaxSettingsService taxSettings)
        {
            _repository = repository;
            _taxSettings = taxSettings;
            _clientApiService = clientApiService;
            _currencyService = currencyService;
            _calculator = calculator;
            _paymentModeService = paymentModeService;
            _branchService = branchService;
            _accountingApiService = accountingApiService;
            _exchangeRateService = exchangeRateService;
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
                TaxTreatment = dto.TaxTreatment,
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
                        DiscountType = item.DiscountType,
                        DiscountValue = item.DiscountValue,
                        DiscountAmount = item.DiscountEnabled
                            ? item.DiscountAmount
                            : 0m
                    }).ToList()
            };

            await _taxSettings.ApplyAsync(invoice, dto.Items, cancellationToken);

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

            // A retry never creates a new invoice number. It only ensures that
            // the already-issued invoice reached Accounting.
            if (invoice.Status == SalesInvoiceStatus.Issued)
            {
                var issuedBranch =
                    await _branchService.GetByNameAsync(
                        invoice.Branch,
                        cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The invoice branch no longer exists.");

                await PostToAccountingAsync(
                    invoice,
                    issuedBranch,
                    cancellationToken);

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

            await _taxSettings.ValidateDraftAsync(invoice, cancellationToken);

            var originalTotals = (invoice.SubTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.GrandTotal);
            _calculator.Calculate(invoice, currency.DecimalPlaces);

            if (originalTotals != (invoice.SubTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.GrandTotal))
            {
                throw new InvalidOperationException("Draft totals no longer match the service lines. Review the draft before issuing.");
            }

            invoice.TaxTreatment =
                ResolveTaxTreatment(
                    invoice.TaxTreatment,
                    branch.CountryCode,
                    invoice.TaxTotal);

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

            await PostToAccountingAsync(
                issued,
                branch,
                cancellationToken);

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

        private async Task PostToAccountingAsync(
            SalesInvoice invoice,
            BranchMaster branch,
            CancellationToken cancellationToken)
        {
            if (!branch.AccountingCompanyId.HasValue)
            {
                throw new InvalidOperationException(
                    "Link the invoice branch to an accounting company before issuing.");
            }

            var company =
                await _accountingApiService.GetCompanyAsync(
                    branch.AccountingCompanyId.Value,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The linked accounting company does not exist.");

            var exchangeRate =
                await _exchangeRateService.GetRateAsync(
                    invoice.Currency,
                    company.Currency,
                    invoice.InvoiceDate,
                    cancellationToken);

            var taxTreatment =
                ResolveTaxTreatment(
                    invoice.TaxTreatment,
                    branch.CountryCode,
                    invoice.TaxTotal);

            var invoiceNumber =
                Clean(invoice.InvoiceNumber);

            if (invoiceNumber.Length == 0)
            {
                throw new InvalidOperationException(
                    "The issued invoice number is missing.");
            }

            var posting =
                new SalesInvoiceAccountingPostingDto
                {
                    CompanyId = company.Id,
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    Branch = invoice.Branch,
                    Currency = invoice.Currency,
                    ExchangeRate = exchangeRate,
                    CustomerId = invoice.ClientId,
                    CustomerName = invoice.ClientName,
                    NetAmount =
                        invoice.SubTotal -
                        invoice.DiscountTotal,
                    TotalAmount = invoice.GrandTotal,
                    Taxes = BuildTaxPostings(
                        taxTreatment,
                        invoice.TaxTotal)
                };

            await _accountingApiService.PostSalesInvoiceAsync(
                posting,
                cancellationToken);
        }

        private static List<SalesInvoiceAccountingTaxDto>
            BuildTaxPostings(
                SalesInvoiceTaxTreatment taxTreatment,
                decimal taxTotal)
        {
            if (
                taxTreatment == SalesInvoiceTaxTreatment.NoTax ||
                taxTotal == 0)
            {
                return new();
            }

            if (taxTreatment == SalesInvoiceTaxTreatment.UaeVat)
            {
                return new()
                {
                    CreateTaxPosting(
                        "OUTPUT_VAT",
                        "Output VAT on sales invoice",
                        taxTotal)
                };
            }

            if (taxTreatment == SalesInvoiceTaxTreatment.IndiaIgst)
            {
                return new()
                {
                    CreateTaxPosting(
                        "OUTPUT_IGST",
                        "Output IGST on sales invoice",
                        taxTotal)
                };
            }

            var cgst = decimal.Round(
                taxTotal / 2m,
                4,
                MidpointRounding.AwayFromZero);

            var sgst = taxTotal - cgst;

            return new()
            {
                CreateTaxPosting(
                    "OUTPUT_CGST",
                    "Output CGST on sales invoice",
                    cgst),

                CreateTaxPosting(
                    "OUTPUT_SGST",
                    "Output SGST on sales invoice",
                    sgst)
            };
        }

        private static SalesInvoiceAccountingTaxDto
            CreateTaxPosting(
                string systemKey,
                string description,
                decimal amount)
        {
            return new SalesInvoiceAccountingTaxDto
            {
                SystemKey = systemKey,
                Description = description,
                Amount = amount
            };
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
                UsesTaxSettings = invoice.UsesTaxSettings,
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
                TaxTreatment = invoice.TaxTreatment,
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
                        DiscountType = item.DiscountType,
                        DiscountValue = item.DiscountValue,
                        DiscountAmount = item.DiscountEnabled
                            ? item.DiscountAmount
                            : 0m,
                        TaxRate = item.TaxRate,
                        TaxSettingId = item.TaxSettingId,
                        TaxSettingSnapshotJson = item.TaxSettingSnapshotJson,
                        LineSubTotal = item.LineSubTotal,
                        TaxAmount = item.TaxAmount,
                        LineTotal = item.LineTotal
                    }).ToList()
            };
        }

        private static SalesInvoiceTaxTreatment
            ResolveTaxTreatment(
                SalesInvoiceTaxTreatment? selected,
                string countryCode,
                decimal taxTotal)
        {
            if (taxTotal == 0)
            {
                return SalesInvoiceTaxTreatment.NoTax;
            }

            var country =
                countryCode
                    .Trim()
                    .ToUpperInvariant();

            if (country == "AE")
            {
                if (
                    selected.HasValue &&
                    selected.Value !=
                    SalesInvoiceTaxTreatment.UaeVat)
                {
                    throw new ArgumentException(
                        "A taxable UAE invoice must use UAE VAT tax treatment.");
                }

                return SalesInvoiceTaxTreatment.UaeVat;
            }

            if (country == "IN")
            {
                if (
                    selected !=
                        SalesInvoiceTaxTreatment.IndiaCgstSgst &&
                    selected !=
                        SalesInvoiceTaxTreatment.IndiaIgst)
                {
                    throw new ArgumentException(
                        "Select India CGST + SGST for an intrastate sale or India IGST for an interstate sale before issuing.");
                }

                return selected.Value;
            }

            throw new ArgumentException(
                "Tax treatment is not configured for the branch country.");
        }
    }
}
