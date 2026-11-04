using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class PaymentReceivedPostingService
        : IPaymentReceivedPostingService
    {
        private const string AccountsReceivableKey =
            "ACCOUNTS_RECEIVABLE";

        private const string CashOnHandKey =
            "CASH_ON_HAND";

        private const string BankChargesKey =
            "BANK_CHARGES";

        private const string ForeignExchangeGainKey =
            "FOREIGN_EXCHANGE_GAIN";

        private const string ForeignExchangeLossKey =
            "FOREIGN_EXCHANGE_LOSS";

        private readonly IJournalEntryRepository _repository;

        public PaymentReceivedPostingService(
            IJournalEntryRepository repository)
        {
            _repository = repository;
        }

        public async Task<PaymentReceivedPostingResponseDto> PostAsync(
            PostPaymentReceivedDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var sourceDocumentId =
                dto.PaymentId.ToString(
                    CultureInfo.InvariantCulture);

            var existing =
                await _repository.GetBySourceDocumentAsync(
                    dto.CompanyId,
                    JournalEntrySource.PaymentReceived,
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

            var accountsReceivable =
                (await _repository.GetAccountsBySystemKeysAsync(
                    dto.CompanyId,
                    new[] { AccountsReceivableKey },
                    cancellationToken))
                .SingleOrDefault()
                ?? throw new ValidationException(
                    "Accounts Receivable is not configured for this company.");

            ValidatePostingAccount(
                accountsReceivable,
                true);

            Account debitAccount;

            if (dto.IsCash)
            {
                debitAccount =
                    (await _repository.GetAccountsBySystemKeysAsync(
                        dto.CompanyId,
                        new[] { CashOnHandKey },
                        cancellationToken))
                    .SingleOrDefault()
                    ?? throw new ValidationException(
                        "Cash on Hand is not configured for this company.");

                ValidatePostingAccount(
                    debitAccount,
                    true);
            }
            else
            {
                var accountId =
                    dto.BankAccountingAccountId
                    ?? throw new ValidationException(
                        "The bank account is not linked to an accounting ledger.");

                debitAccount =
                    (await _repository.GetAccountsAsync(
                        dto.CompanyId,
                        new[] { accountId },
                        cancellationToken))
                    .SingleOrDefault()
                    ?? throw new ValidationException(
                        "The linked bank accounting ledger does not exist for this company.");

                ValidateBankAccount(
                    debitAccount);
            }

            var amount = RoundAmount(
                dto.Amount);

            var exchangeRate = RoundRate(
                dto.ExchangeRate);

            var sourceCurrency =
                dto.Currency
                    .Trim()
                    .ToUpperInvariant();

            var settlementCurrency =
                dto.SettlementCurrency
                    .Trim()
                    .ToUpperInvariant();

            var settlementAmount =
                RoundAmount(
                    dto.SettlementAmount);

            var bankChargeAmount =
                RoundAmount(
                    dto.BankChargeAmount);

            var description =
                $"Payment {dto.PaymentNumber.Trim()} - " +
                dto.CustomerName.Trim();

            var useSettlementAccounting =
                !string.Equals(
                    sourceCurrency,
                    settlementCurrency,
                    StringComparison.OrdinalIgnoreCase) ||
                bankChargeAmount > 0 ||
                settlementAmount != amount;

            List<JournalEntryLine> lines;
            decimal journalExchangeRate;
            decimal journalTotal;
            string journalCurrency;

            if (useSettlementAccounting)
            {
                if (dto.IsCash)
                {
                    throw new ValidationException(
                        "Cash payments cannot use a different settlement currency.");
                }

                if (!string.Equals(
                    settlementCurrency,
                    company.Currency,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException(
                        "The receiving bank currency must match the accounting company base currency for converted payments.");
                }

                var invoiceId =
                    dto.SalesInvoiceId
                    ?? throw new ValidationException(
                        "The source sales invoice is required for a converted payment.");

                var invoiceJournal =
                    await _repository.GetBySourceDocumentAsync(
                        dto.CompanyId,
                        JournalEntrySource.SalesInvoice,
                        invoiceId.ToString(
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

                var receivableAmount =
                    RoundAmount(
                        amount *
                        invoiceJournal.ExchangeRate);

                var proceedsAndCharges =
                    RoundAmount(
                        settlementAmount +
                        bankChargeAmount);

                var difference =
                    RoundAmount(
                        proceedsAndCharges -
                        receivableAmount);

                var requiredKeys =
                    new List<string>();

                if (bankChargeAmount > 0)
                {
                    requiredKeys.Add(
                        BankChargesKey);
                }

                if (difference > 0)
                {
                    requiredKeys.Add(
                        ForeignExchangeGainKey);
                }
                else if (difference < 0)
                {
                    requiredKeys.Add(
                        ForeignExchangeLossKey);
                }

                var settlementAccounts =
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
                    if (!settlementAccounts.TryGetValue(
                        key,
                        out var settlementAccount))
                    {
                        throw new ValidationException(
                            $"Accounting account {key} is not configured. Run the standard Chart of Accounts setup again.");
                    }

                    ValidatePostingAccount(
                        settlementAccount,
                        true);
                }

                lines = new List<JournalEntryLine>();

                lines.Add(
                    CreateLine(
                        dto.CompanyId,
                        lines.Count + 1,
                        debitAccount.Id,
                        description,
                        settlementAmount,
                        0m,
                        1m,
                        dto.CustomerId));

                if (bankChargeAmount > 0)
                {
                    lines.Add(
                        CreateLine(
                            dto.CompanyId,
                            lines.Count + 1,
                            settlementAccounts[
                                BankChargesKey].Id,
                            "Bank charges - " +
                            description,
                            bankChargeAmount,
                            0m,
                            1m,
                            dto.CustomerId));
                }

                if (difference < 0)
                {
                    lines.Add(
                        CreateLine(
                            dto.CompanyId,
                            lines.Count + 1,
                            settlementAccounts[
                                ForeignExchangeLossKey].Id,
                            "Foreign exchange loss - " +
                            description,
                            Math.Abs(difference),
                            0m,
                            1m,
                            dto.CustomerId));
                }

                lines.Add(
                    CreateLine(
                        dto.CompanyId,
                        lines.Count + 1,
                        accountsReceivable.Id,
                        description,
                        0m,
                        receivableAmount,
                        1m,
                        dto.CustomerId));

                if (difference > 0)
                {
                    lines.Add(
                        CreateLine(
                            dto.CompanyId,
                            lines.Count + 1,
                            settlementAccounts[
                                ForeignExchangeGainKey].Id,
                            "Foreign exchange gain - " +
                            description,
                            0m,
                            difference,
                            1m,
                            dto.CustomerId));
                }

                journalCurrency =
                    settlementCurrency;

                journalExchangeRate = 1m;

                journalTotal =
                    Math.Max(
                        receivableAmount,
                        proceedsAndCharges);
            }
            else
            {
                lines =
                [
                    CreateLine(
                        dto.CompanyId,
                        1,
                        debitAccount.Id,
                        description,
                        amount,
                        0m,
                        exchangeRate,
                        dto.CustomerId),

                    CreateLine(
                        dto.CompanyId,
                        2,
                        accountsReceivable.Id,
                        description,
                        0m,
                        amount,
                        exchangeRate,
                        dto.CustomerId)
                ];

                journalCurrency =
                    sourceCurrency;

                journalExchangeRate =
                    exchangeRate;

                journalTotal = amount;
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

                    Currency =
                        journalCurrency,

                    ExchangeRate =
                        journalExchangeRate,

                    Narration =
                        $"Payment received from {dto.CustomerName.Trim()}",

                    Reference =
                        string.IsNullOrWhiteSpace(
                            dto.Reference)
                            ? dto.PaymentNumber.Trim()
                            : dto.Reference.Trim(),

                    Source =
                        JournalEntrySource.PaymentReceived,

                    SourceDocumentId =
                        sourceDocumentId,

                    SourceDocumentNumber =
                        dto.PaymentNumber.Trim(),

                    Status =
                        JournalEntryStatus.Posted,

                    TotalDebit =
                        journalTotal,

                    TotalCredit =
                        journalTotal,

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
            PostPaymentReceivedDto dto)
        {
            if (dto.PaymentDate == default)
            {
                throw new ValidationException(
                    "Payment date is required.");
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

            var settlementCurrency =
                dto.SettlementCurrency?.Trim() ??
                string.Empty;

            if (
                settlementCurrency.Length != 3 ||
                !settlementCurrency.All(char.IsLetter))
            {
                throw new ValidationException(
                    "Settlement currency must be a valid three-letter code.");
            }

            if (dto.SettlementAmount <= 0)
            {
                throw new ValidationException(
                    "Settlement amount must be greater than zero.");
            }

            if (dto.BankChargeAmount < 0)
            {
                throw new ValidationException(
                    "Bank charges cannot be negative.");
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
                    $"Account {account.Code} - {account.Name} is not connected to a system function.");
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
                    $"Account {account.Code} - {account.Name} is not a valid bank posting account.");
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
                $"PR-{entryDate:yyyyMMdd}-" +
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

        private static PaymentReceivedPostingResponseDto
            ToResponse(
                JournalEntry journalEntry,
                bool alreadyExisted)
        {
            return new PaymentReceivedPostingResponseDto
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
