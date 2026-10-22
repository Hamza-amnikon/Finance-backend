using System.Data;
using BankingService.API.Data;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BankingService.API.Repositories
{
    public class BankReconciliationRepository
        : IBankReconciliationRepository
    {
        private readonly BankingDbContext _context;

        public BankReconciliationRepository(
            BankingDbContext context)
        {
            _context = context;
        }

        public IQueryable<BankReconciliation> Query()
        {
            return _context.BankReconciliations
                .AsNoTracking();
        }

        public IQueryable<BankStatementLine> QueryLines()
        {
            return _context.BankStatementLines
                .AsNoTracking();
        }

        public async Task<BankReconciliation?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.BankReconciliations
                .Include(reconciliation => reconciliation.BankAccount)
                .Include(reconciliation => reconciliation.Lines)
                    .ThenInclude(line => line.MatchedBankTransaction)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    reconciliation => reconciliation.Id == id,
                    cancellationToken);
        }

        public async Task<BankReconciliation?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return await _context.BankReconciliations
                .Include(reconciliation => reconciliation.BankAccount)
                .Include(reconciliation => reconciliation.Lines)
                    .ThenInclude(line => line.MatchedBankTransaction)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    reconciliation => reconciliation.RequestId == requestId,
                    cancellationToken);
        }

        public async Task AddAsync(
            BankReconciliation reconciliation,
            CancellationToken cancellationToken)
        {
            await _context.BankReconciliations.AddAsync(
                reconciliation,
                cancellationToken);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
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