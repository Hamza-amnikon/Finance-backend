using System.ComponentModel.DataAnnotations;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class FinancialReportService : IFinancialReportService
    {
        private readonly AccountingDbContext _context;

        public FinancialReportService(AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<FinancialReportDto> GetAsync(
            int companyId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken)
        {
            if (companyId < 1 || fromDate > toDate)
            {
                throw new ValidationException(
                    "Select a company and a valid date range.");
            }

            var company = await _context.Companies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == companyId,
                    cancellationToken);

            if (company == null)
            {
                throw new KeyNotFoundException(
                    "Accounting company was not found.");
            }

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(row =>
                    row.CompanyId == companyId &&
                    row.IsPostingAccount)
                .Select(row => new
                {
                    row.Id,
                    row.Code,
                    row.Name,
                    row.Type,
                    row.SystemKey,
                    ParentSystemKey = row.ParentAccount != null
                        ? row.ParentAccount.SystemKey
                        : null
                })
                .ToListAsync(cancellationToken);

            var movements = await _context.JournalEntryLines
                .AsNoTracking()
                .Where(row =>
                    row.CompanyId == companyId &&
                    row.JournalEntry.EntryDate <= toDate &&
                    (row.JournalEntry.Status == JournalEntryStatus.Posted ||
                     row.JournalEntry.Status == JournalEntryStatus.Reversed))
                .GroupBy(row => row.AccountId)
                .Select(group => new
                {
                    AccountId = group.Key,
                    BeforeDebit = group.Sum(row =>
                        row.JournalEntry.EntryDate < fromDate
                            ? row.BaseDebitAmount
                            : 0m),
                    BeforeCredit = group.Sum(row =>
                        row.JournalEntry.EntryDate < fromDate
                            ? row.BaseCreditAmount
                            : 0m),
                    PeriodDebit = group.Sum(row =>
                        row.JournalEntry.EntryDate >= fromDate
                            ? row.BaseDebitAmount
                            : 0m),
                    PeriodCredit = group.Sum(row =>
                        row.JournalEntry.EntryDate >= fromDate
                            ? row.BaseCreditAmount
                            : 0m)
                })
                .ToListAsync(cancellationToken);

            var byAccount = movements.ToDictionary(row => row.AccountId);
            var result = new FinancialReportDto
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                Currency = company.Currency,
                FromDate = fromDate,
                ToDate = toDate
            };

            foreach (var account in accounts.OrderBy(row => row.Code))
            {
                byAccount.TryGetValue(account.Id, out var movement);

                var opening = (movement?.BeforeDebit ?? 0m) -
                    (movement?.BeforeCredit ?? 0m);
                var debit = movement?.PeriodDebit ?? 0m;
                var credit = movement?.PeriodCredit ?? 0m;
                var closing = opening + debit - credit;

                var row = new FinancialReportAccountDto
                {
                    Code = account.Code,
                    Name = account.Name
                };

                switch (account.Type)
                {
                    case AccountType.Asset:
                        row.Amount = closing;
                        result.AssetAccounts.Add(row);
                        result.Assets += closing;
                        break;
                    case AccountType.Liability:
                        row.Amount = -closing;
                        result.LiabilityAccounts.Add(row);
                        result.Liabilities -= closing;
                        break;
                    case AccountType.Equity:
                        row.Amount = -closing;
                        result.EquityAccounts.Add(row);
                        result.Equity -= closing;
                        break;
                    case AccountType.Income:
                        row.Amount = credit - debit;
                        result.IncomeAccounts.Add(row);
                        result.Income += row.Amount;
                        result.UnclosedEarnings -= closing;
                        break;
                    case AccountType.Expense:
                        row.Amount = debit - credit;
                        result.ExpenseAccounts.Add(row);
                        result.Expenses += row.Amount;
                        result.UnclosedEarnings -= closing;
                        break;
                }

                if (account.SystemKey == "CASH_ON_HAND" ||
                    account.ParentSystemKey == "BANK_ACCOUNTS")
                {
                    result.CashOpening += opening;
                    result.CashDebits += debit;
                    result.CashCredits += credit;
                    result.CashClosing += closing;

                    var cashRow = new FinancialReportAccountDto
                    {
                        Code = account.Code,
                        Name = account.Name,
                        Amount = closing
                    };

                    result.CashAccounts.Add(cashRow);

                    if (account.ParentSystemKey == "BANK_ACCOUNTS")
                    {
                        result.BankAccounts.Add(cashRow);
                    }
                }
            }

            result.Profit = result.Income - result.Expenses;
            result.BalanceDifference = result.Assets -
                result.Liabilities -
                result.Equity -
                result.UnclosedEarnings;

            return result;
        }
    }
}
