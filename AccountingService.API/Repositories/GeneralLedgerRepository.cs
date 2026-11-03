using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Repositories
{
    public class GeneralLedgerRepository
        : IGeneralLedgerRepository
    {
        private readonly AccountingDbContext _context;

        public GeneralLedgerRepository(
            AccountingDbContext context)
        {
            _context = context;
        }

        public Task<Account?> GetAccountAsync(
            int accountId,
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.Accounts
                .AsNoTracking()
                .Include(account =>
                    account.Company)
                .FirstOrDefaultAsync(
                    account =>
                        account.Id == accountId &&
                        account.CompanyId ==
                        companyId,
                    cancellationToken);
        }

        public async Task<decimal>
            GetOpeningBalanceAsync(
                GeneralLedgerQueryDto query,
                CancellationToken cancellationToken)
        {
            var lines =
                CreateLedgerQuery(
                    query)
                .Where(line =>
                    line.JournalEntry.EntryDate <
                    query.FromDate);

            var openingBalance =
                await lines
                    .Select(line =>
                        (decimal?)(
                            line.BaseDebitAmount -
                            line.BaseCreditAmount
                        ))
                    .SumAsync(
                        cancellationToken);

            return openingBalance ?? 0m;
        }

        public async Task<GeneralLedgerPageData>
            GetPageAsync(
                GeneralLedgerQueryDto query,
                CancellationToken cancellationToken)
        {
            var lines =
                CreateLedgerQuery(
                    query)
                .Where(line =>
                    line.JournalEntry.EntryDate >=
                    query.FromDate &&
                    line.JournalEntry.EntryDate <=
                    query.ToDate);

            var total =
                await lines.CountAsync(
                    cancellationToken);

            var periodDebit =
                await lines
                    .Select(line =>
                        (decimal?)
                        line.BaseDebitAmount)
                    .SumAsync(
                        cancellationToken)
                ?? 0m;

            var periodCredit =
                await lines
                    .Select(line =>
                        (decimal?)
                        line.BaseCreditAmount)
                    .SumAsync(
                        cancellationToken)
                ?? 0m;

            var offset =
                (query.Page - 1) *
                query.PageSize;

            var orderedLines =
                lines
                    .OrderBy(line =>
                        line.JournalEntry
                            .EntryDate)
                    .ThenBy(line =>
                        line.JournalEntryId)
                    .ThenBy(line =>
                        line.LineNumber)
                    .ThenBy(line =>
                        line.Id);

            decimal amountBeforePage = 0m;

            if (offset > 0)
            {
                amountBeforePage =
                    await orderedLines
                        .Take(offset)
                        .Select(line =>
                            (decimal?)(
                                line.BaseDebitAmount -
                                line.BaseCreditAmount
                            ))
                        .SumAsync(
                            cancellationToken)
                    ?? 0m;
            }

            var rows =
                await orderedLines
                    .Skip(offset)
                    .Take(query.PageSize)
                    .Select(line =>
                        new GeneralLedgerDataRow
                        {
                            JournalEntryId =
                                line.JournalEntryId,

                            JournalEntryLineId =
                                line.Id,

                            LineNumber =
                                line.LineNumber,

                            EntryNumber =
                                line.JournalEntry
                                    .EntryNumber,

                            EntryDate =
                                line.JournalEntry
                                    .EntryDate,

                            Branch =
                                line.JournalEntry
                                    .Branch,

                            Source =
                                line.JournalEntry
                                    .Source,

                            Status =
                                line.JournalEntry
                                    .Status,

                            SourceDocumentNumber =
                                line.JournalEntry
                                    .SourceDocumentNumber,

                            Reference =
                                line.JournalEntry
                                    .Reference,

                            Narration =
                                line.JournalEntry
                                    .Narration,

                            Description =
                                line.Description,

                            TransactionCurrency =
                                line.JournalEntry
                                    .Currency,

                            ExchangeRate =
                                line.JournalEntry
                                    .ExchangeRate,

                            TransactionDebit =
                                line.DebitAmount,

                            TransactionCredit =
                                line.CreditAmount,

                            BaseDebit =
                                line.BaseDebitAmount,

                            BaseCredit =
                                line.BaseCreditAmount,

                            CostCenter =
                                line.CostCenter,

                            CounterpartyType =
                                line.CounterpartyType,

                            CounterpartyId =
                                line.CounterpartyId
                        })
                    .ToListAsync(
                        cancellationToken);

            return new GeneralLedgerPageData
            {
                Rows = rows,
                Total = total,

                AmountBeforePage =
                    amountBeforePage,

                PeriodDebit =
                    periodDebit,

                PeriodCredit =
                    periodCredit
            };
        }

        private IQueryable<JournalEntryLine>
            CreateLedgerQuery(
                GeneralLedgerQueryDto query)
        {
            var lines =
                _context.JournalEntryLines
                    .AsNoTracking()
                    .Where(line =>
                        line.CompanyId ==
                        query.CompanyId &&
                        line.AccountId ==
                        query.AccountId &&
                        (
                            line.JournalEntry.Status ==
                            JournalEntryStatus.Posted ||
                            line.JournalEntry.Status ==
                            JournalEntryStatus.Reversed
                        ));

            if (!string.IsNullOrWhiteSpace(
                query.Branch))
            {
                var branch =
                    query.Branch.Trim();

                lines = lines.Where(line =>
                    line.JournalEntry.Branch ==
                    branch);
            }

            if (!string.IsNullOrWhiteSpace(
                query.CostCenter))
            {
                var costCenter =
                    query.CostCenter.Trim();

                lines = lines.Where(line =>
                    line.CostCenter ==
                    costCenter);
            }

            if (!string.IsNullOrWhiteSpace(
                query.Search))
            {
                var search =
                    query.Search.Trim();

                lines = lines.Where(line =>
                    line.JournalEntry.EntryNumber
                        .Contains(search) ||
                    line.JournalEntry.Narration
                        .Contains(search) ||
                    (
                        line.JournalEntry.Reference !=
                        null &&
                        line.JournalEntry.Reference
                            .Contains(search)
                    ) ||
                    (
                        line.JournalEntry
                            .SourceDocumentNumber !=
                        null &&
                        line.JournalEntry
                            .SourceDocumentNumber
                            .Contains(search)
                    ) ||
                    line.Description
                        .Contains(search));
            }

            return lines;
        }
    }
}