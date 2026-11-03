using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class SalesInvoicePostingService
        : ISalesInvoicePostingService
    {
        private const string AccountsReceivableKey =
            "ACCOUNTS_RECEIVABLE";

        private const string SalesRevenueKey =
            "SALES_REVENUE";

        private static readonly HashSet<string>
            AllowedTaxSystemKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "OUTPUT_VAT",
                "OUTPUT_CGST",
                "OUTPUT_SGST",
                "OUTPUT_IGST"
            };

        private readonly IJournalEntryRepository
            _repository;

        public SalesInvoicePostingService(
            IJournalEntryRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesInvoicePostingResponseDto>
            PostAsync(
                PostSalesInvoiceDto dto,
                CancellationToken cancellationToken)
        {
            Validate(dto);

            var sourceDocumentId =
                dto.InvoiceId.ToString(
                    CultureInfo.InvariantCulture);

            var existing =
                await _repository
                    .GetBySourceDocumentAsync(
                        dto.CompanyId,
                        JournalEntrySource.SalesInvoice,
                        sourceDocumentId,
                        cancellationToken);

            if (existing != null)
            {
                return ToResponse(
                    existing,
                    true);
            }

            var companyExists =
                await _repository.CompanyExistsAsync(
                    dto.CompanyId,
                    cancellationToken);

            if (!companyExists)
            {
                throw new ValidationException(
                    "The accounting company does not exist.");
            }

            var exchangeRate =
                RoundRate(dto.ExchangeRate);

            var netAmount =
                RoundAmount(dto.NetAmount);

            var totalAmount =
                RoundAmount(dto.TotalAmount);

            var taxPostings =
                NormalizeTaxes(dto.Taxes);

            var taxTotal =
                RoundAmount(
                    taxPostings.Sum(
                        tax => tax.Amount));

            if (
                RoundAmount(netAmount + taxTotal) !=
                totalAmount)
            {
                throw new ValidationException(
                    "Invoice net amount plus tax must equal the total amount.");
            }

            var systemKeys =
                new[]
                {
                    AccountsReceivableKey,
                    SalesRevenueKey
                }
                .Concat(
                    taxPostings.Select(
                        tax => tax.SystemKey))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            var accounts =
                await _repository
                    .GetAccountsBySystemKeysAsync(
                        dto.CompanyId,
                        systemKeys,
                        cancellationToken);

            var accountsBySystemKey =
                accounts
                    .Where(account =>
                        account.SystemKey != null)
                    .ToDictionary(
                        account =>
                            account.SystemKey!,
                        StringComparer.OrdinalIgnoreCase);

            foreach (var systemKey in systemKeys)
            {
                if (!accountsBySystemKey.TryGetValue(
                        systemKey,
                        out var account))
                {
                    throw new ValidationException(
                        $"Accounting account {systemKey} is not configured for this company.");
                }

                ValidateAutomaticPostingAccount(
                    account);
            }

            var accountsReceivable =
                accountsBySystemKey[
                    AccountsReceivableKey];

            var salesRevenue =
                accountsBySystemKey[
                    SalesRevenueKey];

            var lines =
                new List<JournalEntryLine>
                {
                    CreateLine(
                        dto.CompanyId,
                        1,
                        accountsReceivable.Id,
                        $"Invoice {dto.InvoiceNumber} - {dto.CustomerName.Trim()}",
                        totalAmount,
                        0m,
                        exchangeRate,
                        dto.CustomerId),

                    CreateLine(
                        dto.CompanyId,
                        2,
                        salesRevenue.Id,
                        $"Sales revenue - invoice {dto.InvoiceNumber}",
                        0m,
                        netAmount,
                        exchangeRate,
                        dto.CustomerId)
                };

            foreach (var tax in taxPostings)
            {
                var account =
                    accountsBySystemKey[
                        tax.SystemKey];

                lines.Add(
                    CreateLine(
                        dto.CompanyId,
                        lines.Count + 1,
                        account.Id,
                        tax.Description,
                        0m,
                        tax.Amount,
                        exchangeRate,
                        dto.CustomerId));
            }

            var now = DateTime.UtcNow;

            var journalEntry =
                new JournalEntry
                {
                    CompanyId = dto.CompanyId,

                    EntryNumber =
                        GenerateEntryNumber(
                            dto.InvoiceDate),

                    EntryDate = dto.InvoiceDate,

                    Branch = dto.Branch.Trim(),

                    Currency =
                        dto.Currency
                            .Trim()
                            .ToUpperInvariant(),

                    ExchangeRate = exchangeRate,

                    Narration =
                        $"Sales invoice {dto.InvoiceNumber} issued to {dto.CustomerName.Trim()}",

                    Reference =
                        dto.InvoiceNumber.Trim(),

                    Source =
                        JournalEntrySource.SalesInvoice,

                    SourceDocumentId =
                        sourceDocumentId,

                    SourceDocumentNumber =
                        dto.InvoiceNumber.Trim(),

                    Status =
                        JournalEntryStatus.Posted,

                    TotalDebit = totalAmount,

                    TotalCredit = totalAmount,

                    Lines = lines,

                    CreatedDate = now,

                    PostedDate = now
                };

            await _repository.AddAsync(
                journalEntry,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            return ToResponse(
                journalEntry,
                false);
        }

        private static void Validate(
            PostSalesInvoiceDto dto)
        {
            if (dto.InvoiceDate == default)
            {
                throw new ValidationException(
                    "Invoice date is required.");
            }

            if (dto.CustomerId == Guid.Empty)
            {
                throw new ValidationException(
                    "Customer ID is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Branch))
            {
                throw new ValidationException(
                    "Branch is required.");
            }

            var currency =
                dto.Currency?.Trim() ??
                string.Empty;

            if (
                currency.Length != 3 ||
                !currency.All(char.IsLetter))
            {
                throw new ValidationException(
                    "Currency must be a valid three-letter code.");
            }

            if (dto.ExchangeRate <= 0)
            {
                throw new ValidationException(
                    "Exchange rate must be greater than zero.");
            }
        }

        private static List<SalesInvoiceTaxPostingDto>
            NormalizeTaxes(
                IEnumerable<SalesInvoiceTaxPostingDto> taxes)
        {
            var normalized =
                new List<SalesInvoiceTaxPostingDto>();

            foreach (var tax in taxes)
            {
                var systemKey =
                    tax.SystemKey
                        .Trim()
                        .ToUpperInvariant();

                if (!AllowedTaxSystemKeys.Contains(
                        systemKey))
                {
                    throw new ValidationException(
                        $"Tax system key {systemKey} is not supported.");
                }

                var amount =
                    RoundAmount(tax.Amount);

                if (amount <= 0)
                {
                    throw new ValidationException(
                        "Tax amount must be greater than zero.");
                }

                normalized.Add(
                    new SalesInvoiceTaxPostingDto
                    {
                        SystemKey = systemKey,
                        Description =
                            tax.Description.Trim(),
                        Amount = amount
                    });
            }

            return normalized
                .GroupBy(
                    tax => tax.SystemKey,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    new SalesInvoiceTaxPostingDto
                    {
                        SystemKey = group.Key,
                        Description =
                            group.First().Description,
                        Amount =
                            RoundAmount(
                                group.Sum(
                                    tax => tax.Amount))
                    })
                .ToList();
        }

        private static void
            ValidateAutomaticPostingAccount(
                Account account)
        {
            if (!account.IsActive)
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} is inactive.");
            }

            if (!account.IsPostingAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} is not a posting account.");
            }

            if (string.IsNullOrWhiteSpace(
                    account.SystemKey))
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} is not connected to a system function.");
            }
        }

        private static JournalEntryLine CreateLine(
            int companyId,
            int lineNumber,
            int accountId,
            string description,
            decimal debitAmount,
            decimal creditAmount,
            decimal exchangeRate,
            Guid customerId)
        {
            return new JournalEntryLine
            {
                CompanyId = companyId,
                LineNumber = lineNumber,
                AccountId = accountId,
                Description = description,
                DebitAmount = debitAmount,
                CreditAmount = creditAmount,
                BaseDebitAmount =
                    RoundAmount(
                        debitAmount *
                        exchangeRate),
                BaseCreditAmount =
                    RoundAmount(
                        creditAmount *
                        exchangeRate),
                CounterpartyType = "Customer",
                CounterpartyId =
                    customerId.ToString("N")
            };
        }

        private static string GenerateEntryNumber(
            DateOnly entryDate)
        {
            return
                $"SI-{entryDate:yyyyMMdd}-" +
                Guid.NewGuid()
                    .ToString("N")[..12]
                    .ToUpperInvariant();
        }

        private static decimal RoundAmount(
            decimal amount)
        {
            return decimal.Round(
                amount,
                4,
                MidpointRounding.AwayFromZero);
        }

        private static decimal RoundRate(
            decimal exchangeRate)
        {
            return decimal.Round(
                exchangeRate,
                8,
                MidpointRounding.AwayFromZero);
        }

        private static SalesInvoicePostingResponseDto
            ToResponse(
                JournalEntry journalEntry,
                bool alreadyExisted)
        {
            return new SalesInvoicePostingResponseDto
            {
                JournalEntryId =
                    journalEntry.Id,
                EntryNumber =
                    journalEntry.EntryNumber,
                Status =
                    journalEntry.Status.ToString(),
                AlreadyExisted =
                    alreadyExisted
            };
        }
    }
}
