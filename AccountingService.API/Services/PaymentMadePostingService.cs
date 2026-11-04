using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class PaymentMadePostingService
        : IPaymentMadePostingService
    {
        private const string AccountsPayableKey =
            "ACCOUNTS_PAYABLE";

        private const string CashOnHandKey =
            "CASH_ON_HAND";

        private const string ForeignExchangeGainKey =
            "FOREIGN_EXCHANGE_GAIN";

        private const string ForeignExchangeLossKey =
            "FOREIGN_EXCHANGE_LOSS";

        private readonly IJournalEntryRepository _repository;

        public PaymentMadePostingService(
            IJournalEntryRepository repository)
        {
            _repository = repository;
        }

        public async Task<PaymentMadePostingResponseDto> PostAsync(
            PostPaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var sourceDocumentId =
                dto.PaymentId.ToString(
                    CultureInfo.InvariantCulture);

            var existing =
                await _repository.GetBySourceDocumentAsync(
                    dto.CompanyId,
                    JournalEntrySource.PaymentMade,
                    sourceDocumentId,
                    cancellationToken);

            if (existing != null)
            {
                return ToResponse(
                    existing,
                    true);
            }

            var company =
                await _repository.GetCompanyAsync(
                    dto.CompanyId,
                    cancellationToken)
                ?? throw new ValidationException(
                    "The accounting company does not exist.");

            var accountsPayable =
                (await _repository.GetAccountsBySystemKeysAsync(
                    dto.CompanyId,
                    new[] { AccountsPayableKey },
                    cancellationToken))
                .SingleOrDefault()
                ?? throw new ValidationException(
                    "Accounts Payable is not configured for this company.");

            ValidatePostingAccount(
                accountsPayable,
                true);

            Account paymentAccount;

            if (dto.IsCash)
            {
                paymentAccount =
                    (await _repository.GetAccountsBySystemKeysAsync(
                        dto.CompanyId,
                        new[] { CashOnHandKey },
                        cancellationToken))
                    .SingleOrDefault()
                    ?? throw new ValidationException(
                        "Cash on Hand is not configured for this company.");

                ValidatePostingAccount(
                    paymentAccount,
                    true);
            }
            else
            {
                var accountId =
                    dto.BankAccountingAccountId
                    ?? throw new ValidationException(
                        "The bank account is not linked to an accounting ledger.");

                paymentAccount =
                    (await _repository.GetAccountsAsync(
                        dto.CompanyId,
                        new[] { accountId },
                        cancellationToken))
                    .SingleOrDefault()
                    ?? throw new ValidationException(
                        "The linked bank accounting ledger does not exist for this company.");

                ValidateBankAccount(
                    paymentAccount);
            }

            var billJournal =
                await _repository.GetBySourceDocumentAsync(
                    dto.CompanyId,
                    JournalEntrySource.PurchaseBill,
                    dto.BillId.ToString(
                        CultureInfo.InvariantCulture),
                    cancellationToken)
                ?? throw new ValidationException(
                    "The source purchase bill accounting entry is unavailable.");

            if (billJournal.Status !=
                JournalEntryStatus.Posted)
            {
                throw new ValidationException(
                    "The source purchase bill accounting entry is not posted.");
            }

            var sourceCurrency =
                dto.Currency
                    .Trim()
                    .ToUpperInvariant();

            var settlementCurrency =
                dto.SettlementCurrency
                    .Trim()
                    .ToUpperInvariant();

            var amount =
                RoundAmount(
                    dto.Amount);

            var settlementAmount =
                RoundAmount(
                    dto.SettlementAmount);

            var exchangeRate =
                RoundRate(
                    dto.ExchangeRate);

            var description =
                $"Payment {dto.PaymentNumber.Trim()} - " +
                dto.VendorName.Trim();

            List<JournalEntryLine> lines;
            decimal journalTotal;
            decimal journalExchangeRate;
            string journalCurrency;

            if (
                string.Equals(
                    sourceCurrency,
                    company.Currency,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    settlementCurrency,
                    company.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (settlementAmount != amount)
                {
                    throw new ValidationException(
                        "The bank amount must equal the supplier " +
                        "payment amount when both currencies are the same.");
                }

                lines =
                [
                    CreateLine(
                        dto.CompanyId,
                        1,
                        accountsPayable.Id,
                        description,
                        amount,
                        0m,
                        exchangeRate,
                        dto.VendorId),

                    CreateLine(
                        dto.CompanyId,
                        2,
                        paymentAccount.Id,
                        description,
                        0m,
                        amount,
                        exchangeRate,
                        dto.VendorId)
                ];

                journalTotal = amount;
                journalExchangeRate = exchangeRate;
                journalCurrency = sourceCurrency;
            }
            else
            {
                var settlementExchangeRate =
                    string.Equals(
                        settlementCurrency,
                        company.Currency,
                        StringComparison.OrdinalIgnoreCase)
                        ? 1m
                        : string.Equals(
                            settlementCurrency,
                            sourceCurrency,
                            StringComparison.OrdinalIgnoreCase)
                            ? exchangeRate
                            : 0m;

                if (settlementExchangeRate <= 0)
                {
                    throw new ValidationException(
                        "The paying bank currency must match either " +
                        "the supplier payment currency or the " +
                        "accounting company base currency.");
                }

                var payableAmount =
                    RoundAmount(
                        amount *
                        billJournal.ExchangeRate);

                var bankAmount =
                    RoundAmount(
                        settlementAmount *
                        settlementExchangeRate);

                var difference =
                    RoundAmount(
                        bankAmount -
                        payableAmount);

                var requiredKeys =
                    new List<string>();

                if (difference > 0)
                {
                    requiredKeys.Add(
                        ForeignExchangeLossKey);
                }
                else if (difference < 0)
                {
                    requiredKeys.Add(
                        ForeignExchangeGainKey);
                }

                var exchangeAccounts =
                    (await _repository
                        .GetAccountsBySystemKeysAsync(
                            dto.CompanyId,
                            requiredKeys,
                            cancellationToken))
                    .Where(account =>
                        account.SystemKey != null)
                    .ToDictionary(
                        account =>
                            account.SystemKey!,
                        StringComparer.OrdinalIgnoreCase);

                foreach (var key in requiredKeys)
                {
                    if (!exchangeAccounts.TryGetValue(
                        key,
                        out var exchangeAccount))
                    {
                        throw new ValidationException(
                            $"Accounting account {key} is not configured. " +
                            "Run the standard Chart of Accounts setup again.");
                    }

                    ValidatePostingAccount(
                        exchangeAccount,
                        true);
                }

                lines = new List<JournalEntryLine>
                {
                    CreateLine(
                        dto.CompanyId,
                        1,
                        accountsPayable.Id,
                        description,
                        payableAmount,
                        0m,
                        1m,
                        dto.VendorId)
                };

                if (difference > 0)
                {
                    lines.Add(
                        CreateLine(
                            dto.CompanyId,
                            lines.Count + 1,
                            exchangeAccounts[
                                ForeignExchangeLossKey].Id,
                            "Foreign exchange loss - " +
                            description,
                            difference,
                            0m,
                            1m,
                            dto.VendorId));
                }

                lines.Add(
                    CreateLine(
                        dto.CompanyId,
                        lines.Count + 1,
                        paymentAccount.Id,
                        description,
                        0m,
                        bankAmount,
                        1m,
                        dto.VendorId));

                if (difference < 0)
                {
                    lines.Add(
                        CreateLine(
                            dto.CompanyId,
                            lines.Count + 1,
                            exchangeAccounts[
                                ForeignExchangeGainKey].Id,
                            "Foreign exchange gain - " +
                            description,
                            0m,
                            Math.Abs(difference),
                            1m,
                            dto.VendorId));
                }

                journalTotal =
                    Math.Max(
                        payableAmount,
                        bankAmount);

                journalExchangeRate = 1m;
                journalCurrency =
                    company.Currency;
            }

            var now = DateTime.UtcNow;

            var journalEntry =
                new JournalEntry
                {
                    CompanyId = dto.CompanyId,

                    EntryNumber =
                        GenerateEntryNumber(
                            dto.PaymentDate),

                    EntryDate = dto.PaymentDate,

                    Branch = dto.Branch.Trim(),

                    Currency = journalCurrency,

                    ExchangeRate =
                        journalExchangeRate,

                    Narration =
                        $"Payment made to {dto.VendorName.Trim()}",

                    Reference =
                        string.IsNullOrWhiteSpace(
                            dto.Reference)
                            ? dto.PaymentNumber.Trim()
                            : dto.Reference.Trim(),

                    Source =
                        JournalEntrySource.PaymentMade,

                    SourceDocumentId =
                        sourceDocumentId,

                    SourceDocumentNumber =
                        dto.PaymentNumber.Trim(),

                    Status =
                        JournalEntryStatus.Posted,

                    TotalDebit = journalTotal,

                    TotalCredit = journalTotal,

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
            PostPaymentMadeDto dto)
        {
            if (dto.PaymentDate == default)
            {
                throw new ValidationException(
                    "Payment date is required.");
            }

            if (dto.VendorId == Guid.Empty)
            {
                throw new ValidationException(
                    "Vendor ID is required.");
            }

            if (string.IsNullOrWhiteSpace(
                dto.Branch))
            {
                throw new ValidationException(
                    "Branch is required.");
            }

            if (dto.ExchangeRate <= 0)
            {
                throw new ValidationException(
                    "Exchange rate must be greater than zero.");
            }

            if (!dto.IsCash &&
                !dto.BankAccountingAccountId.HasValue)
            {
                throw new ValidationException(
                    "The bank account is not linked to an accounting ledger.");
            }
        }

        private static void ValidatePostingAccount(
            Account account,
            bool requireSystemAccount)
        {
            if (!account.IsActive ||
                !account.IsPostingAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} is not an active posting account.");
            }

            if (requireSystemAccount &&
                string.IsNullOrWhiteSpace(
                    account.SystemKey))
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} " +
                    "is not connected to a system function.");
            }
        }

        private static void ValidateBankAccount(
            Account account)
        {
            ValidatePostingAccount(
                account,
                false);

            if (
                account.Type != AccountType.Asset ||
                account.IsControlAccount ||
                account.IsSystemAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} - {account.Name} " +
                    "is not a valid bank posting account.");
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
            Guid vendorId)
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
                CounterpartyType = "Vendor",
                CounterpartyId =
                    vendorId.ToString("N")
            };
        }

        private static string GenerateEntryNumber(
            DateOnly entryDate)
        {
            return
                $"PM-{entryDate:yyyyMMdd}-" +
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

        private static PaymentMadePostingResponseDto
            ToResponse(
                JournalEntry journalEntry,
                bool alreadyExisted)
        {
            return new PaymentMadePostingResponseDto
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
