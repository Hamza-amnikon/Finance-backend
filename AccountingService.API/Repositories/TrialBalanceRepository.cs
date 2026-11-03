using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Repositories
{
    public class TrialBalanceRepository
        : ITrialBalanceRepository
    {
        private readonly AccountingDbContext
            _context;

        public TrialBalanceRepository(
            AccountingDbContext context)
        {
            _context = context;
        }

        public Task<AccountingCompanyLookup?>
            GetCompanyAsync(
                int companyId,
                CancellationToken cancellationToken)
        {
            return _context.Companies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    company =>
                        company.Id ==
                        companyId,
                    cancellationToken);
        }

        public async Task<TrialBalancePageData>
            GetPageAsync(
                TrialBalanceQueryDto query,
                CancellationToken cancellationToken)
        {
            var asAtDate =
                query.AsAtDate ??
                DateOnly.FromDateTime(
                    DateTime.UtcNow);

            var journalLines =
                _context.JournalEntryLines
                    .AsNoTracking()
                    .Where(line =>
                        line.CompanyId ==
                            query.CompanyId &&
                        line.JournalEntry.EntryDate <=
                            asAtDate &&
                        (
                            line.JournalEntry.Status ==
                                JournalEntryStatus.Posted ||
                            line.JournalEntry.Status ==
                                JournalEntryStatus.Reversed
                        ));

            if (
                !string.IsNullOrWhiteSpace(
                    query.Branch)
            )
            {
                var branch =
                    query.Branch.Trim();

                journalLines =
                    journalLines.Where(line =>
                        line.JournalEntry.Branch ==
                        branch);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    query.CostCenter)
            )
            {
                var costCenter =
                    query.CostCenter.Trim();

                journalLines =
                    journalLines.Where(line =>
                        line.CostCenter ==
                        costCenter);
            }

            var movementRows =
                await journalLines
                    .GroupBy(
                        line =>
                            line.AccountId)
                    .Select(group => new
                    {
                        AccountId =
                            group.Key,

                        DebitMovement =
                            group.Sum(
                                line =>
                                    line.BaseDebitAmount),

                        CreditMovement =
                            group.Sum(
                                line =>
                                    line.BaseCreditAmount)
                    })
                    .ToListAsync(
                        cancellationToken);

            var movementByAccount =
                movementRows.ToDictionary(
                    row =>
                        row.AccountId);

            var accounts =
                await _context.Accounts
                    .AsNoTracking()
                    .Include(account =>
                        account.ParentAccount)
                    .Where(account =>
                        account.CompanyId ==
                            query.CompanyId &&
                        account.IsPostingAccount)
                    .ToListAsync(
                        cancellationToken);

            var allRows =
                accounts
                    .Select(account =>
                    {
                        movementByAccount.TryGetValue(
                            account.Id,
                            out var movement);

                        var debitMovement =
                            movement?.DebitMovement ??
                            0m;

                        var creditMovement =
                            movement?.CreditMovement ??
                            0m;

                        var balance =
                            debitMovement -
                            creditMovement;

                        return new TrialBalanceDataRow
                        {
                            AccountId =
                                account.Id,

                            AccountCode =
                                account.Code,

                            AccountName =
                                account.Name,

                            AccountType =
                                account.Type,

                            NormalBalance =
                                account.NormalBalance,

                            ParentAccountId =
                                account.ParentAccountId,

                            ParentAccountCode =
                                account.ParentAccount?.Code,

                            ParentAccountName =
                                account.ParentAccount?.Name,

                            IsControlAccount =
                                account.IsControlAccount,

                            IsSystemAccount =
                                account.IsSystemAccount,

                            DebitMovement =
                                debitMovement,

                            CreditMovement =
                                creditMovement,

                            DebitBalance =
                                balance > 0m
                                    ? balance
                                    : 0m,

                            CreditBalance =
                                balance < 0m
                                    ? -balance
                                    : 0m
                        };
                    })
                    .ToList();

            var totalDebitMovement =
                allRows.Sum(row =>
                    row.DebitMovement);

            var totalCreditMovement =
                allRows.Sum(row =>
                    row.CreditMovement);

            var totalDebitBalance =
                allRows.Sum(row =>
                    row.DebitBalance);

            var totalCreditBalance =
                allRows.Sum(row =>
                    row.CreditBalance);

            IEnumerable<TrialBalanceDataRow>
                filteredRows = allRows;

            if (!query.IncludeZeroBalances)
            {
                filteredRows =
                    filteredRows.Where(row =>
                        row.DebitBalance != 0m ||
                        row.CreditBalance != 0m);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    query.Search)
            )
            {
                var search =
                    query.Search.Trim();

                filteredRows =
                    filteredRows.Where(row =>
                        row.AccountCode.Contains(
                            search,
                            StringComparison
                                .OrdinalIgnoreCase) ||
                        row.AccountName.Contains(
                            search,
                            StringComparison
                                .OrdinalIgnoreCase) ||
                        (
                            row.ParentAccountCode != null &&
                            row.ParentAccountCode.Contains(
                                search,
                                StringComparison
                                    .OrdinalIgnoreCase)
                        ) ||
                        (
                            row.ParentAccountName != null &&
                            row.ParentAccountName.Contains(
                                search,
                                StringComparison
                                    .OrdinalIgnoreCase)
                        ));
            }

            var total =
                filteredRows.Count();

            var offset =
                ((long)query.Page - 1) *
                query.PageSize;

            IReadOnlyList<TrialBalanceDataRow>
                rows;

            if (offset >= total)
            {
                rows = [];
            }
            else
            {
                rows =
                    filteredRows
                        .OrderBy(
                            row =>
                                row.AccountCode)
                        .ThenBy(
                            row =>
                                row.AccountId)
                        .Skip(
                            (int)offset)
                        .Take(
                            query.PageSize)
                        .ToList();
            }

            return new TrialBalancePageData
            {
                Rows = rows,
                Total = total,

                TotalDebitMovement =
                    totalDebitMovement,

                TotalCreditMovement =
                    totalCreditMovement,

                TotalDebitBalance =
                    totalDebitBalance,

                TotalCreditBalance =
                    totalCreditBalance
            };
        }
    }
}
