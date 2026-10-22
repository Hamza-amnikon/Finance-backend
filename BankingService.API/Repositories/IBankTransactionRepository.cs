using BankingService.API.Models;

namespace BankingService.API.Repositories
{
    public interface IBankTransactionRepository
    {
        IQueryable<BankTransaction> Query();

        Task<BankTransaction?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankTransaction?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<BankTransaction?> GetReversalAsync(
            int transactionId,
            CancellationToken cancellationToken);

        Task AddAsync(
            BankTransaction transaction,
            CancellationToken cancellationToken);

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}