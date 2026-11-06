using System.ComponentModel.DataAnnotations;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class TaxReportService : ITaxReportService
    {
        private readonly AccountingDbContext _context;

        public TaxReportService(AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<TaxReportDto> GetAsync(
            int companyId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken)
        {
            if (companyId < 1 || fromDate == default ||
                toDate == default || fromDate > toDate)
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

            var taxKeys = company.Country == "AE"
                ? new[] { "INPUT_VAT", "OUTPUT_VAT" }
                : company.Country == "IN"
                    ? new[]
                    {
                        "INPUT_CGST", "INPUT_SGST", "INPUT_IGST",
                        "OUTPUT_CGST", "OUTPUT_SGST", "OUTPUT_IGST",
                        "TDS_RECEIVABLE", "TDS_PAYABLE"
                    }
                    : [];

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(row => row.CompanyId == companyId &&
                    row.IsPostingAccount && row.SystemKey != null &&
                    taxKeys.Contains(row.SystemKey))
                .Select(row => new
                {
                    row.Id,
                    row.Code,
                    row.Name,
                    row.SystemKey
                })
                .OrderBy(row => row.Code)
                .ToListAsync(cancellationToken);

            var accountIds = accounts.Select(row => row.Id).ToArray();
            var movements = await _context.JournalEntryLines
                .AsNoTracking()
                .Where(row => row.CompanyId == companyId &&
                    accountIds.Contains(row.AccountId) &&
                    row.JournalEntry.EntryDate <= toDate &&
                    (row.JournalEntry.Status == JournalEntryStatus.Posted ||
                     row.JournalEntry.Status == JournalEntryStatus.Reversed))
                .GroupBy(row => row.AccountId)
                .Select(group => new
                {
                    AccountId = group.Key,
                    BeforeDebit = group.Sum(row =>
                        row.JournalEntry.EntryDate < fromDate
                            ? row.BaseDebitAmount : 0m),
                    BeforeCredit = group.Sum(row =>
                        row.JournalEntry.EntryDate < fromDate
                            ? row.BaseCreditAmount : 0m),
                    PeriodDebit = group.Sum(row =>
                        row.JournalEntry.EntryDate >= fromDate
                            ? row.BaseDebitAmount : 0m),
                    PeriodCredit = group.Sum(row =>
                        row.JournalEntry.EntryDate >= fromDate
                            ? row.BaseCreditAmount : 0m)
                })
                .ToListAsync(cancellationToken);

            var byAccount = movements.ToDictionary(row => row.AccountId);
            var result = new TaxReportDto
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                Country = company.Country,
                Currency = company.Currency,
                FromDate = fromDate,
                ToDate = toDate
            };

            foreach (var account in accounts)
            {
                byAccount.TryGetValue(account.Id, out var movement);
                var opening = (movement?.BeforeDebit ?? 0m) -
                    (movement?.BeforeCredit ?? 0m);
                var debit = movement?.PeriodDebit ?? 0m;
                var credit = movement?.PeriodCredit ?? 0m;

                result.Accounts.Add(new TaxReportAccountDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    SystemKey = account.SystemKey ?? string.Empty,
                    OpeningBalance = opening,
                    PeriodDebit = debit,
                    PeriodCredit = credit,
                    ClosingBalance = opening + debit - credit
                });
            }

            return result;
        }
    }
}
