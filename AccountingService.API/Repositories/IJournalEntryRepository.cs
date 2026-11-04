using AccountingService.API.DTOs;
using AccountingService.API.Models;

namespace AccountingService.API.Repositories
{
    public interface IJournalEntryRepository
    {
        Task<(
            IReadOnlyList<JournalEntry> JournalEntries,
            int Total)> GetAllAsync(
                JournalEntryQueryDto query,
                CancellationToken cancellationToken);

        Task<JournalEntry?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken);

        Task<JournalEntry?> GetBySourceDocumentAsync(
            int companyId,
            JournalEntrySource source,
            string sourceDocumentId,
            CancellationToken cancellationToken);

        Task<bool> CompanyExistsAsync(
            int companyId,
            CancellationToken cancellationToken);

        Task<AccountingCompanyLookup?> GetCompanyAsync(
            int companyId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<Account>> GetAccountsAsync(
            int companyId,
            IEnumerable<int> accountIds,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<Account>>
            GetAccountsBySystemKeysAsync(
                int companyId,
                IEnumerable<string> systemKeys,
                CancellationToken cancellationToken);

        Task<IReadOnlyList<Account>> GetAccountsByCodesAsync(
            int companyId,
            IEnumerable<string> accountCodes,
            CancellationToken cancellationToken);

        Task<bool> ReversalExistsAsync(
            int journalEntryId,
            int companyId,
            CancellationToken cancellationToken);

        Task AddAsync(
            JournalEntry journalEntry,
            CancellationToken cancellationToken);

        void RemoveLines(
            IEnumerable<JournalEntryLine> lines);

        void SetOriginalRowVersion(
            JournalEntry journalEntry,
            byte[] rowVersion);

        Task SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}
