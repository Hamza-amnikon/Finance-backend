using System.ComponentModel.DataAnnotations;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

namespace SalesInvoiceService.API.Services
{
    public class SalesCreditNoteService : ISalesCreditNoteService
    {
        private readonly ISalesCreditNoteRepository _repository;
        private readonly ISalesCreditNoteCalculator _calculator;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly IBranchService _branchService;
        private readonly IAccountingApiService _accountingApiService;

        public SalesCreditNoteService(
            ISalesCreditNoteRepository repository,
            ISalesCreditNoteCalculator calculator,
            ICurrencyRepository currencyRepository,
            IBranchService branchService,
            IAccountingApiService accountingApiService)
        {
            _repository = repository;
            _calculator = calculator;
            _currencyRepository = currencyRepository;
            _branchService = branchService;
            _accountingApiService = accountingApiService;
        }

        public async Task<SalesCreditNoteResponseDto> PreviewAsync(
            CreateSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            ValidateCreateRequest(dto);

            var invoice = await _repository.GetInvoiceAsync(
                dto.SalesInvoiceId,
                cancellationToken);

            if (invoice == null)
            {
                throw new KeyNotFoundException(
                    "Invoice not found.");
            }

            if (invoice.Status != SalesInvoiceStatus.Issued)
            {
                throw new InvalidOperationException(
                    "Credit notes require an issued invoice.");
            }

            if (dto.CreditNoteDate < invoice.InvoiceDate)
            {
                throw new ArgumentException(
                    "Credit note date cannot be before the invoice date.");
            }

            // Read the original invoice currency configuration.
            var currency = await _currencyRepository.GetByCodeAsync(
                invoice.Currency,
                cancellationToken);

            if (currency == null)
            {
                throw new InvalidOperationException(
                    "The original invoice currency is not configured.");
            }

            var issuedItems =
                await _repository.GetIssuedCreditItemsAsync(
                    invoice.Id,
                    cancellationToken);

            var creditNote = CreateModel(dto);
            creditNote.SalesInvoice = invoice;

            _calculator.Calculate(
                creditNote,
                invoice,
                issuedItems,
                currency.DecimalPlaces);

            // Preview does not save anything.
            return ToResponse(creditNote);
        }

        public async Task<SalesCreditNoteResponseDto> CreateAsync(
            CreateSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            ValidateCreateRequest(dto);

            var creditNote = CreateModel(dto);

            // The repository calculates under the invoice lock
            // and handles retries using RequestId.
            var created = await _repository.CreateDraftAsync(
                creditNote,
                cancellationToken);

            return ToResponse(created);
        }

        public async Task<SalesCreditNoteResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                throw new ArgumentException(
                    "A valid credit note ID is required.");
            }

            var creditNote = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            return creditNote == null
                ? null
                : ToResponse(creditNote);
        }

        public async Task<(
            List<SalesCreditNoteResponseDto> Items,
            int Total)> GetPagedAsync(
                int page,
                int pageSize,
                string? search,
                SalesCreditNoteStatus? status,
                CancellationToken cancellationToken,
                string? branch = null)
        {
            var result = await _repository.GetPagedAsync(
                page,
                pageSize,
                search,
                status,
                cancellationToken,
                branch);

            return (
                result.Items.Select(ToResponse).ToList(),
                result.Total);
        }

        public async Task<SalesCreditNoteResponseDto?> IssueAsync(
            int id,
            IssueSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                throw new ArgumentException(
                    "A valid credit note ID is required.");
            }

            ValidateDto(dto);

            // The repository rechecks amounts, assigns the number,
            // and issues the credit note in one transaction.
            var issued = await _repository.IssueAsync(
                id,
                dto.RowVersion,
                cancellationToken);

            if (issued != null)
            {
                await PostToAccountingAsync(
                    issued,
                    cancellationToken);
            }

            return issued == null
                ? null
                : ToResponse(issued);
        }

        private async Task PostToAccountingAsync(
            SalesCreditNote creditNote,
            CancellationToken cancellationToken)
        {
            var invoice = creditNote.SalesInvoice
                ?? throw new InvalidOperationException(
                    "Credit note invoice details were not loaded.");

            var branch =
                await _branchService.GetByNameAsync(
                    invoice.Branch,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The invoice branch no longer exists.");

            if (!branch.AccountingCompanyId.HasValue)
            {
                throw new InvalidOperationException(
                    "Link the invoice branch to an accounting company before issuing the credit note.");
            }

            var company =
                await _accountingApiService.GetCompanyAsync(
                    branch.AccountingCompanyId.Value,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The linked accounting company does not exist.");

            var creditNoteNumber =
                Clean(creditNote.CreditNoteNumber);

            if (creditNoteNumber.Length == 0)
            {
                throw new InvalidOperationException(
                    "The issued credit note number is missing.");
            }

            var invoiceNumber =
                Clean(invoice.InvoiceNumber);

            if (invoiceNumber.Length == 0)
            {
                throw new InvalidOperationException(
                    "The source invoice number is missing.");
            }

            var posting =
                new SalesCreditNoteAccountingPostingDto
                {
                    CompanyId = company.Id,
                    CreditNoteId = creditNote.Id,
                    CreditNoteNumber = creditNoteNumber,
                    CreditNoteDate = creditNote.CreditNoteDate,
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoiceNumber,
                    Branch = invoice.Branch,
                    Currency = creditNote.Currency,
                    CustomerId = invoice.ClientId,
                    CustomerName = invoice.ClientName,
                    NetAmount =
                        creditNote.SubTotal -
                        creditNote.DiscountTotal,
                    TotalAmount = creditNote.GrandTotal,
                    Taxes = BuildTaxPostings(
                        invoice.TaxTreatment,
                        creditNote.TaxTotal)
                };

            await _accountingApiService
                .PostSalesCreditNoteAsync(
                    posting,
                    cancellationToken);
        }

        private static List<SalesCreditNoteAccountingTaxDto>
            BuildTaxPostings(
                SalesInvoiceTaxTreatment? taxTreatment,
                decimal taxTotal)
        {
            if (taxTotal == 0)
            {
                return new();
            }

            if (!taxTreatment.HasValue ||
                taxTreatment ==
                    SalesInvoiceTaxTreatment.NoTax)
            {
                throw new InvalidOperationException(
                    "The source invoice tax treatment is unavailable.");
            }

            if (taxTreatment ==
                SalesInvoiceTaxTreatment.UaeVat)
            {
                return new()
                {
                    CreateTaxPosting(
                        "OUTPUT_VAT",
                        "Output VAT reversed by sales credit note",
                        taxTotal)
                };
            }

            if (taxTreatment ==
                SalesInvoiceTaxTreatment.IndiaIgst)
            {
                return new()
                {
                    CreateTaxPosting(
                        "OUTPUT_IGST",
                        "Output IGST reversed by sales credit note",
                        taxTotal)
                };
            }

            var cgst = decimal.Round(
                taxTotal / 2m,
                4,
                MidpointRounding.AwayFromZero);

            return new()
            {
                CreateTaxPosting(
                    "OUTPUT_CGST",
                    "Output CGST reversed by sales credit note",
                    cgst),

                CreateTaxPosting(
                    "OUTPUT_SGST",
                    "Output SGST reversed by sales credit note",
                    taxTotal - cgst)
            };
        }

        private static SalesCreditNoteAccountingTaxDto
            CreateTaxPosting(
                string systemKey,
                string description,
                decimal amount)
        {
            return new SalesCreditNoteAccountingTaxDto
            {
                SystemKey = systemKey,
                Description = description,
                Amount = amount
            };
        }

        private static SalesCreditNote CreateModel(
            CreateSalesCreditNoteDto dto)
        {
            return new SalesCreditNote
            {
                RequestId = dto.RequestId,
                SalesInvoiceId = dto.SalesInvoiceId,
                CreditNoteDate = dto.CreditNoteDate,
                Reason = Clean(dto.Reason),
                Notes = Clean(dto.Notes),
                Status = SalesCreditNoteStatus.Draft,

                Items = dto.Items
                    .Select(item => new SalesCreditNoteItem
                    {
                        SalesInvoiceItemId =
                            item.SalesInvoiceItemId,

                        Quantity = item.Quantity,
                        CreditAmount = item.CreditAmount
                    })
                    .ToList()
            };
        }

        private static void ValidateCreateRequest(
            CreateSalesCreditNoteDto dto)
        {
            ValidateDto(dto);

            // Validate nested item DTOs as well.
            foreach (var item in dto.Items)
            {
                ValidateDto(item);
            }
        }

        private static void ValidateDto(object dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var errors = new List<ValidationResult>();

            if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                errors,
                validateAllProperties: true))
            {
                throw new ArgumentException(
                    string.Join(
                        " ",
                        errors.Select(error => error.ErrorMessage)));
            }
        }

        private static SalesCreditNoteResponseDto ToResponse(
            SalesCreditNote creditNote)
        {
            var invoice = creditNote.SalesInvoice
                ?? throw new InvalidOperationException(
                    "Credit note invoice details were not loaded.");

            return new SalesCreditNoteResponseDto
            {
                Id = creditNote.Id,
                RequestId = creditNote.RequestId,
                SalesInvoiceId = creditNote.SalesInvoiceId,
                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                CreditNoteNumber = creditNote.CreditNoteNumber,

                ClientId = invoice.ClientId,
                ClientName = invoice.ClientName,
                CompanyName = invoice.CompanyName,
                BillingAddress = invoice.BillingAddress,
                Branch = invoice.Branch,

                Currency = creditNote.Currency,
                CreditNoteDate = creditNote.CreditNoteDate,
                Reason = creditNote.Reason,
                Notes = creditNote.Notes,

                SubTotal = creditNote.SubTotal,
                DiscountTotal = creditNote.DiscountTotal,
                TaxTotal = creditNote.TaxTotal,
                GrandTotal = creditNote.GrandTotal,

                Status = creditNote.Status,
                CreatedDate = creditNote.CreatedDate,
                IssuedDate = creditNote.IssuedDate,
                RowVersion = creditNote.RowVersion.ToArray(),

                Items = creditNote.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new SalesCreditNoteItemResponseDto
                    {
                        Id = item.Id,
                        SalesInvoiceItemId = item.SalesInvoiceItemId,
                        LineNumber = item.LineNumber,
                        ServiceName = item.ServiceName,
                        Description = item.Description,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        CreditAmount = item.CreditAmount,
                        UnitPrice = item.UnitPrice,
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate,
                        LineSubTotal = item.LineSubTotal,
                        TaxAmount = item.TaxAmount,
                        LineTotal = item.LineTotal
                    })
                    .ToList()
            };
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
