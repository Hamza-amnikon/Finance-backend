using BankingService.API.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace BankingService.API.Repositories
{
    public interface IBankTransferRepository
    {
        IQueryable<BankTransfer> Query();

        Task<BankTransfer?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankTransfer?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task AddAsync(
            BankTransfer transfer,
            CancellationToken cancellationToken);

        Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken);

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}