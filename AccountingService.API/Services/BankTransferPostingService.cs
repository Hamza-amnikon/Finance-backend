using System.ComponentModel.DataAnnotations;
using System.Data;
using AccountingService.API.Data;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class BankTransferPostingService
    {
        private readonly AccountingDbContext _context;

        public BankTransferPostingService(AccountingDbContext context)
        {
            _context = context;
        }

        public Task<List<int>> GetPendingIdsAsync(CancellationToken cancellationToken)
        {
            return _context.Database.SqlQuery<int>($"""
                SELECT t.Id AS Value FROM banking.BankTransfers t
                WHERE t.Status IN (1, 2) AND
                (NOT EXISTS (SELECT 1 FROM accounting.JournalEntries j
                    WHERE j.CompanyId=t.SourceCompanyId AND j.Source=N'BankTransaction'
                    AND j.SourceDocumentId=CONCAT(N'Transfer:',t.Id,N':sent'))
                 OR (t.Status=2 AND NOT EXISTS (SELECT 1 FROM accounting.JournalEntries j
                    WHERE j.CompanyId=t.DestinationCompanyId AND j.Source=N'BankTransaction'
                    AND j.SourceDocumentId=CONCAT(N'Transfer:',t.Id,N':received'))))
                ORDER BY t.Id
                """).ToListAsync(cancellationToken);
        }

        public async Task<int> PostAsync(int transferId, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            // Serialize retries against the same saved transfer, including old transfers.
            var transfer = await _context.Database.SqlQuery<TransferPostingRow>($"""
                SELECT Id,SourceCompanyId,DestinationCompanyId,SourceBankAccountId,
                    DestinationBankAccountId,SourceCurrency,DestinationCurrency,
                    SourceCurrencyDecimalPlaces,DestinationCurrencyDecimalPlaces,
                    SourceAmount,SourceFeeAmount,DestinationAmount,TransferDate,ReceivedDate,
                    Status,Reference,SourceTransactionId,SourceFeeTransactionId,DestinationTransactionId
                FROM banking.BankTransfers WITH (UPDLOCK,HOLDLOCK) WHERE Id={transferId}
                """).SingleOrDefaultAsync(cancellationToken)
                ?? throw new ValidationException("Bank transfer was not found.");

            if (transfer.Status is not (1 or 2))
            {
                throw new ValidationException("Only sent or completed transfers can be posted.");
            }

            ValidateAmount(transfer.SourceAmount, transfer.SourceCurrencyDecimalPlaces);
            ValidateAmount(transfer.SourceFeeAmount, transfer.SourceCurrencyDecimalPlaces, true);
            if (transfer.Status == 2)
            {
                ValidateAmount(transfer.DestinationAmount ?? 0m, transfer.DestinationCurrencyDecimalPlaces);
                if (transfer.ReceivedDate == null)
                {
                    throw new ValidationException("The transfer receipt date is missing.");
                }
                if (transfer.SourceCompanyId == transfer.DestinationCompanyId &&
                    transfer.SourceAmount != transfer.DestinationAmount)
                {
                    throw new ValidationException("A same-company transfer with different sent and received amounts needs an explicit settlement adjustment.");
                }
            }

            var source = await GetBankAsync(
                transfer.SourceBankAccountId, transfer.SourceCompanyId, transfer.SourceCurrency,
                cancellationToken);
            var destination = await GetBankAsync(
                transfer.DestinationBankAccountId, transfer.DestinationCompanyId,
                transfer.DestinationCurrency, cancellationToken);
            if (source.Account.Id == destination.Account.Id)
            {
                throw new ValidationException("Transfer banks cannot share the same ledger account.");
            }

            await ValidateBankTransactionAsync(
                transfer.SourceTransactionId, transfer.SourceCompanyId, transfer.SourceBankAccountId,
                transfer.SourceCurrency, transfer.SourceAmount, transfer.TransferDate, 2, cancellationToken);
            if (transfer.SourceFeeAmount > 0)
            {
                await ValidateBankTransactionAsync(
                    transfer.SourceFeeTransactionId, transfer.SourceCompanyId, transfer.SourceBankAccountId,
                    transfer.SourceCurrency, transfer.SourceFeeAmount, transfer.TransferDate, 2, cancellationToken);
            }
            else if (transfer.SourceFeeTransactionId != null)
            {
                throw new ValidationException("A zero-fee transfer has an unexpected fee transaction.");
            }
            if (transfer.Status == 2)
            {
                await ValidateBankTransactionAsync(
                    transfer.DestinationTransactionId, transfer.DestinationCompanyId,
                    transfer.DestinationBankAccountId, transfer.DestinationCurrency,
                    transfer.DestinationAmount!.Value, transfer.ReceivedDate!.Value, 1, cancellationToken);
            }

            var created = 0;
            var sentKey = $"Transfer:{transfer.Id}:sent";
            var receivedKey = $"Transfer:{transfer.Id}:received";
            var sameCompany = transfer.SourceCompanyId == transfer.DestinationCompanyId;
            if (!await AlreadyPostedAsync(source.Company.Id, sentKey,
                transfer.TransferDate, transfer.SourceCurrency,
                transfer.SourceAmount + transfer.SourceFeeAmount, cancellationToken))
            {
                var clearing = await GetClearingAsync(
                    source.Company, destination.Company, false, sameCompany, cancellationToken);
                var lines = new List<JournalEntryLine>
                {
                    Line(source.Company.Id, clearing.Id, transfer.SourceAmount, 0m, destination.Company.Id),
                    Line(source.Company.Id, source.Account.Id, 0m,
                        transfer.SourceAmount + transfer.SourceFeeAmount, destination.Company.Id)
                };
                if (transfer.SourceFeeAmount > 0)
                {
                    var fee = await _context.Accounts.SingleOrDefaultAsync(
                        row => row.CompanyId == source.Company.Id && row.SystemKey == "BANK_CHARGES",
                        cancellationToken)
                        ?? throw new ValidationException("Bank Charges is not configured for the sending company.");
                    ValidateAccount(fee, source.Company.Id, AccountType.Expense);
                    lines.Add(Line(source.Company.Id, fee.Id, transfer.SourceFeeAmount, 0m, null));
                }
                AddJournal(transfer, source.Company, sentKey, transfer.TransferDate, "sent", lines);
                created++;
            }

            if (transfer.Status == 2 && !await AlreadyPostedAsync(destination.Company.Id,
                receivedKey, transfer.ReceivedDate!.Value, transfer.DestinationCurrency,
                transfer.DestinationAmount!.Value, cancellationToken))
            {
                var clearing = await GetClearingAsync(
                    destination.Company, source.Company, true, sameCompany, cancellationToken);
                AddJournal(transfer, destination.Company, receivedKey,
                    transfer.ReceivedDate.Value, "received",
                    [
                        Line(destination.Company.Id, destination.Account.Id,
                            transfer.DestinationAmount.Value, 0m, source.Company.Id),
                        Line(destination.Company.Id, clearing.Id,
                            0m, transfer.DestinationAmount.Value, source.Company.Id)
                    ]);
                created++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        }

        private async Task<(Account Account, AccountingCompanyLookup Company)> GetBankAsync(
            int bankId, int companyId, string currency, CancellationToken cancellationToken)
        {
            var mapping = await _context.Database.SqlQuery<TransferBankRow>($"""
                SELECT CompanyId,Currency,AccountingAccountId FROM expenses.BankAccounts WHERE Id={bankId}
                """).SingleOrDefaultAsync(cancellationToken);
            var company = await _context.Companies.SingleOrDefaultAsync(
                row => row.Id == companyId, cancellationToken);
            if (mapping == null || company == null || mapping.CompanyId != companyId ||
                mapping.Currency != currency || mapping.AccountingAccountId == null)
            {
                throw new ValidationException("Transfer bank ownership, currency or ledger mapping is invalid.");
            }
            // Never invent historical FX for foreign-currency bank accounts.
            if (company.Currency != currency)
            {
                throw new ValidationException("Transfer posting requires the bank currency to match its company currency; a foreign-currency bank needs historical valuation setup.");
            }
            var account = await _context.Accounts.Include(row => row.ParentAccount)
                .SingleOrDefaultAsync(row => row.Id == mapping.AccountingAccountId, cancellationToken)
                ?? throw new ValidationException("The mapped bank ledger does not exist.");
            ValidateAccount(account, companyId, AccountType.Asset);
            if (account.ParentAccount?.SystemKey != "BANK_ACCOUNTS")
            {
                throw new ValidationException("The mapped ledger must be under Bank Accounts.");
            }
            return (account, company);
        }

        private async Task ValidateBankTransactionAsync(
            int? id, int companyId, int bankId, string currency, decimal amount,
            DateOnly date, int direction, CancellationToken cancellationToken)
        {
            var count = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM banking.BankTransactions
                WHERE Id={id} AND CompanyId={companyId} AND BankAccountId={bankId}
                    AND Currency={currency} AND Amount={amount} AND TransactionDate={date}
                    AND Direction={direction} AND Status=N'Posted'
                """).SingleAsync(cancellationToken);
            if (count != 1)
            {
                throw new ValidationException("The transfer's linked posted bank transaction does not match its saved details.");
            }
        }

        private async Task<bool> AlreadyPostedAsync(
            int companyId, string key, DateOnly date, string currency, decimal amount,
            CancellationToken cancellationToken)
        {
            var existing = await _context.JournalEntries.SingleOrDefaultAsync(
                row => row.CompanyId == companyId && row.Source == JournalEntrySource.BankTransaction &&
                    row.SourceDocumentId == key, cancellationToken);
            if (existing == null)
            {
                return false;
            }
            if (existing.Status != JournalEntryStatus.Posted || existing.EntryDate != date ||
                existing.Currency != currency || existing.TotalDebit != amount || existing.TotalCredit != amount)
            {
                throw new ValidationException("An existing transfer journal needs review; it will not be replaced or reposted.");
            }
            return true;
        }

        private async Task<Account> GetClearingAsync(
            AccountingCompanyLookup company, AccountingCompanyLookup otherCompany,
            bool received, bool sameCompany, CancellationToken cancellationToken)
        {
            var type = received && !sameCompany ? AccountType.Liability : AccountType.Asset;
            var key = sameCompany ? "BANK_TRANSFER_CLEARING" :
                $"BRANCH_DUE_{(received ? "TO" : "FROM")}_{otherCompany.Id}";
            var existing = await _context.Accounts.SingleOrDefaultAsync(
                row => row.CompanyId == company.Id && row.SystemKey == key, cancellationToken);
            if (existing != null)
            {
                ValidateAccount(existing, company.Id, type);
                return existing;
            }
            var parentKey = type == AccountType.Asset ? "CURRENT_ASSETS" : "CURRENT_LIABILITIES";
            var parent = await _context.Accounts.SingleOrDefaultAsync(
                row => row.CompanyId == company.Id && row.SystemKey == parentKey,
                cancellationToken)
                ?? throw new ValidationException("The company chart needs its current asset/liability parent account.");
            if (!parent.IsActive || parent.IsPostingAccount || parent.Type != type)
            {
                throw new ValidationException("The transfer clearing parent is not valid.");
            }
            var code = sameCompany ? "BANK-TRANSFER" : $"BR-{(received ? "TO" : "FROM")}-{otherCompany.Id}";
            if (await _context.Accounts.AnyAsync(
                row => row.CompanyId == company.Id && row.Code == code, cancellationToken))
            {
                throw new ValidationException("The transfer clearing account code is already used. Review the chart mapping.");
            }
            var name = sameCompany ? "Bank transfer clearing" :
                $"Due {(received ? "to" : "from")} branch: {otherCompany.Name}";
            var account = new Account
            {
                CompanyId = company.Id,
                Code = code,
                Name = name.Length > 160 ? name[..160] : name,
                Type = type,
                NormalBalance = type == AccountType.Asset ? AccountNormalBalance.Debit : AccountNormalBalance.Credit,
                ParentAccountId = parent.Id,
                IsPostingAccount = true,
                IsControlAccount = true,
                IsSystemAccount = true,
                SystemKey = key,
                Description = "Internal funding balance. Review interbranch balances together in a common currency at consolidation."
            };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);
            return account;
        }

        private void AddJournal(
            TransferPostingRow transfer, AccountingCompanyLookup company, string key,
            DateOnly date, string action, List<JournalEntryLine> lines)
        {
            var number = $"BT-{transfer.Id}";
            for (var index = 0; index < lines.Count; index++)
            {
                lines[index].LineNumber = index + 1;
                lines[index].Description = $"Bank transfer {number} {action}";
            }
            var total = lines.Sum(row => row.DebitAmount);
            if (total != lines.Sum(row => row.CreditAmount))
            {
                throw new ValidationException("Transfer journal is not balanced.");
            }
            _context.JournalEntries.Add(new JournalEntry
            {
                CompanyId = company.Id,
                EntryNumber = $"BT-{transfer.Id}-{action}",
                EntryDate = date,
                Currency = company.Currency,
                ExchangeRate = 1m,
                Narration = $"Bank transfer {number} {action}. Reference: {transfer.Reference}",
                Reference = transfer.Reference.Length > 100 ? transfer.Reference[..100] : transfer.Reference,
                Source = JournalEntrySource.BankTransaction,
                SourceDocumentId = key,
                SourceDocumentNumber = number,
                Status = JournalEntryStatus.Posted,
                TotalDebit = total,
                TotalCredit = total,
                PostedDate = DateTime.UtcNow,
                Lines = lines
            });
        }

        private static JournalEntryLine Line(
            int companyId, int accountId, decimal debit, decimal credit, int? counterparty)
        {
            return new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = accountId,
                DebitAmount = debit,
                CreditAmount = credit,
                BaseDebitAmount = debit,
                BaseCreditAmount = credit,
                CounterpartyType = counterparty.HasValue ? "BranchCompany" : null,
                CounterpartyId = counterparty?.ToString()
            };
        }

        private static void ValidateAccount(Account account, int companyId, AccountType type)
        {
            if (account.CompanyId != companyId || !account.IsActive || !account.IsPostingAccount || account.Type != type)
            {
                throw new ValidationException("The transfer posting account is inactive, non-posting, or belongs to a different company/type.");
            }
        }

        private static void ValidateAmount(decimal value, int precision, bool allowZero = false)
        {
            if (precision is < 0 or > 4 || value < 0 || (!allowZero && value == 0) ||
                decimal.Round(value, precision) != value)
            {
                throw new ValidationException("The saved transfer amount has invalid currency precision.");
            }
        }
    }

    public class TransferBankRow
    {
        public int CompanyId { get; set; }
        public string Currency { get; set; } = "";
        public int? AccountingAccountId { get; set; }
    }

    public class TransferPostingRow
    {
        public int Id { get; set; }
        public int SourceCompanyId { get; set; }
        public int DestinationCompanyId { get; set; }
        public int SourceBankAccountId { get; set; }
        public int DestinationBankAccountId { get; set; }
        public string SourceCurrency { get; set; } = "";
        public string DestinationCurrency { get; set; } = "";
        public int SourceCurrencyDecimalPlaces { get; set; }
        public int DestinationCurrencyDecimalPlaces { get; set; }
        public decimal SourceAmount { get; set; }
        public decimal SourceFeeAmount { get; set; }
        public decimal? DestinationAmount { get; set; }
        public DateOnly TransferDate { get; set; }
        public DateOnly? ReceivedDate { get; set; }
        public int Status { get; set; }
        public string Reference { get; set; } = "";
        public int? SourceTransactionId { get; set; }
        public int? SourceFeeTransactionId { get; set; }
        public int? DestinationTransactionId { get; set; }
    }
}
