using BankingService.API.Data;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BankingService.API.Repositories
{
    public class BankTransferRepository : IBankTransferRepository
    {
        private readonly BankingDbContext _context;

        public BankTransferRepository(
            BankingDbContext context)
        {
            _context = context;
        }

        public IQueryable<BankTransfer> Query()
        {
            return _context.BankTransfers
                .AsNoTracking();
        }

        public async Task<BankTransfer?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.BankTransfers
                .Include(transfer => transfer.SourceBankAccount)
                .Include(transfer => transfer.DestinationBankAccount)
                .FirstOrDefaultAsync(
                    transfer => transfer.Id == id,
                    cancellationToken);
        }

        public async Task<BankTransfer?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return await _context.BankTransfers
                .Include(transfer => transfer.SourceBankAccount)
                .Include(transfer => transfer.DestinationBankAccount)
                .FirstOrDefaultAsync(
                    transfer => transfer.RequestId == requestId,
                    cancellationToken);
        }

        public async Task AddAsync(
            BankTransfer transfer,
            CancellationToken cancellationToken)
        {
            await _context.BankTransfers.AddAsync(
                transfer,
                cancellationToken);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Database.BeginTransactionAsync(
                cancellationToken);
        }

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            return await _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}