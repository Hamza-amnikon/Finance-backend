using BankingService.API.Data;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Repositories
{
    public class BankTransactionRepository : IBankTransactionRepository
    {
        private readonly BankingDbContext _context;

        public BankTransactionRepository(
            BankingDbContext context)
        {
            _context = context;
        }

        public IQueryable<BankTransaction> Query()
        {
            return _context.BankTransactions
                .AsNoTracking();
        }

        public async Task<BankTransaction?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.BankTransactions
                .Include(transaction => transaction.BankAccount)
                .FirstOrDefaultAsync(
                    transaction => transaction.Id == id,
                    cancellationToken);
        }

        public async Task<BankTransaction?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return await _context.BankTransactions
                .Include(transaction => transaction.BankAccount)
                .FirstOrDefaultAsync(
                    transaction => transaction.RequestId == requestId,
                    cancellationToken);
        }

        public async Task<BankTransaction?> GetReversalAsync(
            int transactionId,
            CancellationToken cancellationToken)
        {
            return await _context.BankTransactions
                .AsNoTracking()
                .Include(transaction => transaction.BankAccount)
                .FirstOrDefaultAsync(
                    transaction =>
                        transaction.ReversalOfTransactionId == transactionId,
                    cancellationToken);
        }

        public async Task AddAsync(
            BankTransaction transaction,
            CancellationToken cancellationToken)
        {
            await _context.BankTransactions.AddAsync(
                transaction,
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