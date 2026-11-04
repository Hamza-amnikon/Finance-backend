using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class SalesCreditNotePostingService
        : ISalesCreditNotePostingService
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

        public SalesCreditNotePostingService(
            IJournalEntryRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesCreditNotePostingResponseDto>
            PostAsync(
                PostSalesCreditNoteDto dto,
                CancellationToken cancellationToken)
        {
            Validate(dto);

            var sourceDocumentId =
                dto.CreditNoteId.ToString(
                    CultureInfo.InvariantCulture);

            var existing =
                await _repository
                    .GetBySourceDocumentAsync(
                        dto.CompanyId,
                        JournalEntrySource.SalesCreditNote,
                        sourceDocumentId,
                        cancellationToken);

            if (existing != null)
            {
                return ToResponse(
                    existing,
                    true);
            }

            var invoiceJournal =
                await _repository
                    .GetBySourceDocumentAsync(
                        dto.CompanyId,
                        JournalEntrySource.SalesInvoice,
                        dto.InvoiceId.ToString(
                            CultureInfo.InvariantCulture),
                        cancellationToken)
                ?? throw new ValidationException(
                    "The source sales invoice accounting entry is unavailable.");

            if (invoiceJournal.Status !=
                JournalEntryStatus.Posted)
            {
                throw new ValidationException(
                    "The source sales invoice accounting entry is not posted.");
            }

            if (!string.Equals(
                    invoiceJournal.Currency,
                    dto.Currency.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "The credit note currency does not match the source invoice accounting entry.");
            }

            var netAmount =
                RoundAmount(dto.NetAmount);

            var totalAmount =
                RoundAmount(dto.TotalAmount);

            var taxes =
                NormalizeTaxes(dto.Taxes);

            if (
                RoundAmount(
                    netAmount +
                    taxes.Sum(tax => tax.Amount)) !=
                totalAmount)
            {
                throw new ValidationException(
                    "Credit note net amount plus tax must equal the total amount.");
            }

            var systemKeys =
                new[]
                {
                    AccountsReceivableKey,
                    SalesRevenueKey
                }
                .Concat(
                    taxes.Select(
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
                        account => account.SystemKey!,
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

            var exchangeRate =
                RoundRate(
                    invoiceJournal.ExchangeRate);

            var lines =
                new List<JournalEntryLine>
                {
                    CreateLine(
                        dto.CompanyId,
                        1,
                        accountsBySystemKey[
                            SalesRevenueKey].Id,
                        $"Sales credit {dto.CreditNoteNumber} - {dto.CustomerName.Trim()}",
                        netAmount,
                        0m,
                        exchangeRate,
                        dto.CustomerId)
                };

            foreach (var tax in taxes)
            {
                lines.Add(
                    CreateLine(
                        dto.CompanyId,
                        lines.Count + 1,
                        accountsBySystemKey[
                            tax.SystemKey].Id,
                        tax.Description,
                        tax.Amount,
                        0m,
                        exchangeRate,
                        dto.CustomerId));
            }

            lines.Add(
                CreateLine(
                    dto.CompanyId,
                    lines.Count + 1,
                    accountsBySystemKey[
                        AccountsReceivableKey].Id,
                    $"Credit note {dto.CreditNoteNumber} against invoice {dto.InvoiceNumber}",
                    0m,
                    totalAmount,
                    exchangeRate,
                    dto.CustomerId));

            var now = DateTime.UtcNow;

            var journalEntry =
                new JournalEntry
                {
                    CompanyId = dto.CompanyId,
                    EntryNumber =
                        GenerateEntryNumber(
                            dto.CreditNoteDate),
                    EntryDate = dto.CreditNoteDate,
                    Branch = dto.Branch.Trim(),
                    Currency =
                        dto.Currency
                            .Trim()
                            .ToUpperInvariant(),
                    ExchangeRate = exchangeRate,
                    Narration =
                        $"Sales credit note {dto.CreditNoteNumber} issued to {dto.CustomerName.Trim()}",
                    Reference =
                        dto.InvoiceNumber.Trim(),
                    Source =
                        JournalEntrySource.SalesCreditNote,
                    SourceDocumentId =
                        sourceDocumentId,
                    SourceDocumentNumber =
                        dto.CreditNoteNumber.Trim(),
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
            PostSalesCreditNoteDto dto)
        {
            if (dto.CreditNoteDate == default)
            {
                throw new ValidationException(
                    "Credit note date is required.");
            }

            if (dto.CustomerId == Guid.Empty)
            {
                throw new ValidationException(
                    "Customer ID is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    dto.Branch))
            {
                throw new ValidationException(
                    "Branch is required.");
            }
        }

        private static List<SalesCreditNoteTaxPostingDto>
            NormalizeTaxes(
                IEnumerable<SalesCreditNoteTaxPostingDto> taxes)
        {
            var normalized =
                taxes.Select(tax =>
                    new SalesCreditNoteTaxPostingDto
                    {
                        SystemKey =
                            tax.SystemKey
                                .Trim()
                                .ToUpperInvariant(),
                        Description =
                            tax.Description.Trim(),
                        Amount =
                            RoundAmount(tax.Amount)
                    })
                .ToList();

            foreach (var tax in normalized)
            {
                if (!AllowedTaxSystemKeys.Contains(
                        tax.SystemKey))
                {
                    throw new ValidationException(
                        $"Tax system key {tax.SystemKey} is not supported.");
                }

                if (tax.Amount <= 0)
                {
                    throw new ValidationException(
                        "Tax amount must be greater than zero.");
                }
            }

            return normalized
                .GroupBy(
                    tax => tax.SystemKey,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    new SalesCreditNoteTaxPostingDto
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
            if (!account.IsActive ||
                !account.IsPostingAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} is not an active posting account.");
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
                $"SC-{entryDate:yyyyMMdd}-" +
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

        private static SalesCreditNotePostingResponseDto
            ToResponse(
                JournalEntry journalEntry,
                bool alreadyExisted)
        {
            return new SalesCreditNotePostingResponseDto
            {
                JournalEntryId = journalEntry.Id,
                EntryNumber = journalEntry.EntryNumber,
                Status = journalEntry.Status.ToString(),
                AlreadyExisted = alreadyExisted
            };
        }
    }
}
