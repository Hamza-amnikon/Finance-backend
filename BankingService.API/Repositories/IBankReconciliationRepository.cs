using BankingService.API.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace BankingService.API.Repositories
{
    public interface IBankReconciliationRepository
    {
        IQueryable<BankReconciliation> Query();

        IQueryable<BankStatementLine> QueryLines();

        Task<BankReconciliation?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankReconciliation?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task AddAsync(
            BankReconciliation reconciliation,
            CancellationToken cancellationToken);

        Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken);

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}