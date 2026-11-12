using Microsoft.EntityFrameworkCore;
using AccountingService.API.Data;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class PurchaseBillPostingService : IPurchaseBillPostingService
    {
        private const string AccountsPayableKey = "ACCOUNTS_PAYABLE";

        private static readonly HashSet<string> AllowedTaxKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "INPUT_VAT",
                "INPUT_CGST",
                "INPUT_SGST",
                "INPUT_IGST"
            };

        private readonly AccountingDbContext _context;
        private readonly IJournalEntryRepository _repository;

        public PurchaseBillPostingService(IJournalEntryRepository repository, AccountingDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        public async Task<PurchaseBillPostingResponseDto> PostAsync(
            PostPurchaseBillDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);
            await using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            var billStatus = await _context.Database.SqlQuery<string>($"""
                SELECT Status AS Value FROM purchases.Bills WITH (UPDLOCK,HOLDLOCK) WHERE Id = {dto.BillId}
                """).SingleOrDefaultAsync(cancellationToken);
            if (billStatus != "Issued")
            {
                throw new ValidationException("Only an issued purchase bill can be posted. Cancelled bills cannot be reposted.");
            }

            var sourceDocumentId = dto.BillId.ToString(CultureInfo.InvariantCulture);
            var existing = await _repository.GetBySourceDocumentAsync(
                dto.CompanyId,
                JournalEntrySource.PurchaseBill,
                sourceDocumentId,
                cancellationToken);

            if (existing != null)
            {
                await transaction.CommitAsync(cancellationToken);
                return ToResponse(existing, true);
            }

            if (!await _repository.CompanyExistsAsync(dto.CompanyId, cancellationToken))
            {
                throw new ValidationException("The accounting company does not exist.");
            }

            var expenses = dto.Expenses
                .Select(item => new PurchaseBillExpensePostingDto
                {
                    AccountCode = item.AccountCode.Trim().ToUpperInvariant(),
                    Description = item.Description.Trim(),
                    Amount = RoundAmount(item.Amount)
                })
                .GroupBy(item => item.AccountCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PurchaseBillExpensePostingDto
                {
                    AccountCode = group.Key,
                    Description = group.First().Description,
                    Amount = RoundAmount(group.Sum(item => item.Amount))
                })
                .ToList();

            if (expenses.Any(item => item.Amount <= 0))
            {
                throw new ValidationException("Each bill expense amount must be greater than zero.");
            }

            var taxes = NormalizeTaxes(dto.Taxes);
            var netAmount = RoundAmount(dto.NetAmount);
            var totalAmount = RoundAmount(dto.TotalAmount);

            if (RoundAmount(expenses.Sum(item => item.Amount)) != netAmount)
            {
                throw new ValidationException("Bill expense lines must equal the bill net amount.");
            }

            if (RoundAmount(netAmount + taxes.Sum(item => item.Amount)) != totalAmount)
            {
                throw new ValidationException("Bill net amount plus tax must equal the total amount.");
            }

            var accountsPayable = (await _repository.GetAccountsBySystemKeysAsync(
                dto.CompanyId,
                new[] { AccountsPayableKey },
                cancellationToken)).SingleOrDefault()
                ?? throw new ValidationException("Accounts Payable is not configured for this company.");

            ValidatePostingAccount(accountsPayable, true);

            var expenseAccounts = await _repository.GetAccountsByCodesAsync(
                dto.CompanyId,
                expenses.Select(item => item.AccountCode),
                cancellationToken);

            var expenseAccountsByCode = expenseAccounts.ToDictionary(
                account => account.Code,
                StringComparer.OrdinalIgnoreCase);

            foreach (var expense in expenses)
            {
                if (!expenseAccountsByCode.TryGetValue(expense.AccountCode, out var account))
                {
                    throw new ValidationException($"Account {expense.AccountCode} does not exist for this company.");
                }

                ValidatePostingAccount(account, false);

                if (account.Type != AccountType.Expense && account.Type != AccountType.Asset)
                {
                    throw new ValidationException(
                        $"Account {account.Code} - {account.Name} must be an Expense or Asset account for a bill line.");
                }
            }

            var taxAccounts = await _repository.GetAccountsBySystemKeysAsync(
                dto.CompanyId,
                taxes.Select(item => item.SystemKey),
                cancellationToken);

            var taxAccountsByKey = taxAccounts
                .Where(account => account.SystemKey != null)
                .ToDictionary(account => account.SystemKey!, StringComparer.OrdinalIgnoreCase);

            foreach (var tax in taxes)
            {
                if (!taxAccountsByKey.TryGetValue(tax.SystemKey, out var account))
                {
                    throw new ValidationException($"Accounting account {tax.SystemKey} is not configured for this company.");
                }

                ValidatePostingAccount(account, true);
            }

            var rate = decimal.Round(dto.ExchangeRate, 8, MidpointRounding.AwayFromZero);
            var lines = new List<JournalEntryLine>();

            foreach (var expense in expenses)
            {
                lines.Add(CreateLine(
                    dto.CompanyId,
                    lines.Count + 1,
                    expenseAccountsByCode[expense.AccountCode].Id,
                    expense.Description,
                    expense.Amount,
                    0m,
                    rate,
                    dto.VendorId));
            }

            foreach (var tax in taxes)
            {
                lines.Add(CreateLine(
                    dto.CompanyId,
                    lines.Count + 1,
                    taxAccountsByKey[tax.SystemKey].Id,
                    tax.Description,
                    tax.Amount,
                    0m,
                    rate,
                    dto.VendorId));
            }

            lines.Add(CreateLine(
                dto.CompanyId,
                lines.Count + 1,
                accountsPayable.Id,
                $"Bill {dto.BillNumber} - {dto.VendorName.Trim()}",
                0m,
                totalAmount,
                rate,
                dto.VendorId));

            var now = DateTime.UtcNow;
            var journal = new JournalEntry
            {
                CompanyId = dto.CompanyId,
                EntryNumber = $"PB-{dto.BillDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
                EntryDate = dto.BillDate,
                Branch = dto.Branch.Trim(),
                Currency = dto.Currency.Trim().ToUpperInvariant(),
                ExchangeRate = rate,
                Narration = $"Purchase bill {dto.BillNumber} issued by {dto.VendorName.Trim()}",
                Reference = dto.BillNumber.Trim(),
                Source = JournalEntrySource.PurchaseBill,
                SourceDocumentId = sourceDocumentId,
                SourceDocumentNumber = dto.BillNumber.Trim(),
                Status = JournalEntryStatus.Posted,
                TotalDebit = totalAmount,
                TotalCredit = totalAmount,
                Lines = lines,
                CreatedDate = now,
                PostedDate = now
            };

            await _repository.AddAsync(journal, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return ToResponse(journal, false);
        }

        private static void Validate(PostPurchaseBillDto dto)
        {
            if (dto.BillDate == default)
            {
                throw new ValidationException("Bill date is required.");
            }

            if (dto.VendorId == Guid.Empty)
            {
                throw new ValidationException("Vendor ID is required.");
            }

            if (dto.ExchangeRate <= 0)
            {
                throw new ValidationException("Exchange rate must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(dto.Branch))
            {
                throw new ValidationException("Branch is required.");
            }
        }

        private static List<PurchaseBillTaxPostingDto> NormalizeTaxes(
            IEnumerable<PurchaseBillTaxPostingDto> values)
        {
            return values.Select(item => new PurchaseBillTaxPostingDto
            {
                SystemKey = item.SystemKey.Trim().ToUpperInvariant(),
                Description = item.Description.Trim(),
                Amount = RoundAmount(item.Amount)
            })
                .Where(item => item.Amount > 0)
                .GroupBy(item => item.SystemKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    if (!AllowedTaxKeys.Contains(group.Key))
                    {
                        throw new ValidationException($"Tax system key {group.Key} is not supported.");
                    }

                    return new PurchaseBillTaxPostingDto
                    {
                        SystemKey = group.Key,
                        Description = group.First().Description,
                        Amount = RoundAmount(group.Sum(item => item.Amount))
                    };
                }).ToList();
        }

        private static void ValidatePostingAccount(Account account, bool requireSystemAccount)
        {
            if (!account.IsActive || !account.IsPostingAccount)
            {
                throw new ValidationException($"Account {account.Code} - {account.Name} is not an active posting account.");
            }

            if (requireSystemAccount && string.IsNullOrWhiteSpace(account.SystemKey))
            {
                throw new ValidationException($"Account {account.Code} - {account.Name} is not connected to a system function.");
            }
        }

        private static JournalEntryLine CreateLine(
            int companyId,
            int lineNumber,
            int accountId,
            string description,
            decimal debit,
            decimal credit,
            decimal exchangeRate,
            Guid vendorId)
        {
            return new JournalEntryLine
            {
                CompanyId = companyId,
                LineNumber = lineNumber,
                AccountId = accountId,
                Description = description,
                DebitAmount = debit,
                CreditAmount = credit,
                BaseDebitAmount = RoundAmount(debit * exchangeRate),
                BaseCreditAmount = RoundAmount(credit * exchangeRate),
                CounterpartyType = "Vendor",
                CounterpartyId = vendorId.ToString("N")
            };
        }

        private static decimal RoundAmount(decimal value) =>
            decimal.Round(value, 4, MidpointRounding.AwayFromZero);

        private static PurchaseBillPostingResponseDto ToResponse(
            JournalEntry journal,
            bool alreadyExisted)
        {
            return new PurchaseBillPostingResponseDto
            {
                JournalEntryId = journal.Id,
                EntryNumber = journal.EntryNumber,
                Status = journal.Status.ToString(),
                AlreadyExisted = alreadyExisted
            };
        }
    }
}
