using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Repositories
{
    public class JournalEntryRepository
        : IJournalEntryRepository
    {
        private readonly AccountingDbContext _context;

        public JournalEntryRepository(
            AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<(
            IReadOnlyList<JournalEntry> JournalEntries,
            int Total)> GetAllAsync(
                JournalEntryQueryDto query,
                CancellationToken cancellationToken)
        {
            var journalEntries = _context.JournalEntries
                .AsNoTracking()
                .Include(journalEntry =>
                    journalEntry.Company)
                .Where(journalEntry =>
                    journalEntry.CompanyId == query.CompanyId);

            if (query.FromDate.HasValue)
            {
                journalEntries = journalEntries.Where(
                    journalEntry =>
                        journalEntry.EntryDate >=
                        query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                journalEntries = journalEntries.Where(
                    journalEntry =>
                        journalEntry.EntryDate <=
                        query.ToDate.Value);
            }

            if (query.Status.HasValue)
            {
                journalEntries = journalEntries.Where(
                    journalEntry =>
                        journalEntry.Status ==
                        query.Status.Value);
            }

            if (query.Source.HasValue)
            {
                journalEntries = journalEntries.Where(
                    journalEntry =>
                        journalEntry.Source ==
                        query.Source.Value);
            }

            if (!string.IsNullOrWhiteSpace(
                query.Branch))
            {
                var branch = query.Branch.Trim();

                journalEntries = journalEntries.Where(
                    journalEntry =>
                        journalEntry.Branch == branch);
            }

            if (!string.IsNullOrWhiteSpace(
                query.Search))
            {
                var search = query.Search.Trim();

                journalEntries = journalEntries.Where(
                    journalEntry =>
                        journalEntry.EntryNumber
                            .Contains(search) ||
                        journalEntry.Narration
                            .Contains(search) ||
                        (journalEntry.Reference != null &&
                         journalEntry.Reference
                             .Contains(search)) ||
                        (journalEntry.SourceDocumentNumber != null &&
                         journalEntry.SourceDocumentNumber
                             .Contains(search)));
            }

            var total = await journalEntries.CountAsync(
                cancellationToken);

            var result = await journalEntries
                .OrderByDescending(journalEntry =>
                    journalEntry.EntryDate)
                .ThenByDescending(journalEntry =>
                    journalEntry.Id)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return (result, total);
        }

        public Task<JournalEntry?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.JournalEntries
                .Include(journalEntry =>
                    journalEntry.Company)
                .Include(journalEntry =>
                    journalEntry.Lines)
                    .ThenInclude(line =>
                        line.Account)
                .Include(journalEntry =>
                    journalEntry.ReversalEntries)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    journalEntry =>
                        journalEntry.Id == id &&
                        journalEntry.CompanyId ==
                        companyId,
                    cancellationToken);
        }

        public Task<JournalEntry?>
            GetBySourceDocumentAsync(
                int companyId,
                JournalEntrySource source,
                string sourceDocumentId,
                CancellationToken cancellationToken)
        {
            return _context.JournalEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    journalEntry =>
                        journalEntry.CompanyId ==
                        companyId &&
                        journalEntry.Source ==
                        source &&
                        journalEntry.SourceDocumentId ==
                        sourceDocumentId,
                    cancellationToken);
        }

        public Task<bool> CompanyExistsAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.Companies
                .AsNoTracking()
                .AnyAsync(
                    company =>
                        company.Id == companyId,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Account>>
            GetAccountsAsync(
                int companyId,
                IEnumerable<int> accountIds,
                CancellationToken cancellationToken)
        {
            var ids = accountIds
                .Distinct()
                .ToList();

            return await _context.Accounts
                .AsNoTracking()
                .Where(account =>
                    account.CompanyId == companyId &&
                    ids.Contains(account.Id))
                .OrderBy(account =>
                    account.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Account>>
            GetAccountsBySystemKeysAsync(
                int companyId,
                IEnumerable<string> systemKeys,
                CancellationToken cancellationToken)
        {
            var keys = systemKeys
                .Select(systemKey =>
                    systemKey.Trim().ToUpper())
                .Distinct()
                .ToList();

            return await _context.Accounts
                .AsNoTracking()
                .Where(account =>
                    account.CompanyId == companyId &&
                    account.SystemKey != null &&
                    keys.Contains(
                        account.SystemKey.ToUpper()))
                .OrderBy(account =>
                    account.Code)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> ReversalExistsAsync(
            int journalEntryId,
            int companyId,
            CancellationToken cancellationToken)
        {
            return _context.JournalEntries
                .AsNoTracking()
                .AnyAsync(
                    journalEntry =>
                        journalEntry.CompanyId ==
                        companyId &&
                        journalEntry
                            .ReversalOfJournalEntryId ==
                        journalEntryId,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetAccountsByCodesAsync(
            int companyId,
            IEnumerable<string> accountCodes,
            CancellationToken cancellationToken)
        {
            var codes = accountCodes
                .Select(code => code.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            return await _context.Accounts
                .AsNoTracking()
                .Where(account =>
                    account.CompanyId == companyId &&
                    codes.Contains(account.Code.ToUpper()))
                .OrderBy(account => account.Code)
                .ToListAsync(cancellationToken);
        }

        public Task AddAsync(
            JournalEntry journalEntry,
            CancellationToken cancellationToken)
        {
            return _context.JournalEntries
                .AddAsync(
                    journalEntry,
                    cancellationToken)
                .AsTask();
        }

        public void RemoveLines(
            IEnumerable<JournalEntryLine> lines)
        {
            _context.JournalEntryLines.RemoveRange(
                lines);
        }

        public void SetOriginalRowVersion(
            JournalEntry journalEntry,
            byte[] rowVersion)
        {
            _context.Entry(journalEntry)
                .Property(entry =>
                    entry.RowVersion)
                .OriginalValue = rowVersion;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            return _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}
