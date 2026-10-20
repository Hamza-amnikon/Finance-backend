using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using ExpenseService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Repositories
{
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly ExpenseDbContext _context;

        public ExpenseRepository(
            ExpenseDbContext context)
        {
            _context = context;
        }

        public IQueryable<Expense> Query(
            ExpenseQueryDto query)
        {
            var rows = _context.Expenses.AsNoTracking();

            if (query.CompanyId.HasValue)
            {
                rows = rows.Where(row =>
                    row.CompanyId == query.CompanyId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var text = query.Search.Trim();

                rows = rows.Where(row =>
                    row.Supplier.Contains(text) ||
                    row.Category.Contains(text) ||
                    row.Reference.Contains(text));
            }

            if (!string.IsNullOrEmpty(query.Status))
            {
                rows = rows.Where(row =>
                    row.Status == query.Status);
            }

            if (query.From.HasValue)
            {
                rows = rows.Where(row =>
                    row.Date >= query.From);
            }

            if (query.To.HasValue)
            {
                rows = rows.Where(row =>
                    row.Date <= query.To);
            }

            return rows;
        }

        public Task<Expense?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.Expenses
                .Include(row => row.Company)
                .Include(row => row.BankAccount)
                .Include(row => row.Receipt)
                .SingleOrDefaultAsync(
                    row => row.Id == id,
                    cancellationToken);
        }

        public Task<Expense?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return _context.Expenses
                .Include(row => row.Company)
                .Include(row => row.BankAccount)
                .SingleOrDefaultAsync(
                    row => row.RequestId == requestId,
                    cancellationToken);
        }

        public Task<Company?> GetCompanyAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.Companies
                .FindAsync([id], cancellationToken)
                .AsTask();
        }

        public Task<BankAccount?> GetAccountAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.BankAccounts
                .FindAsync([id], cancellationToken)
                .AsTask();
        }

        public async Task SaveAsync(
            Expense expense,
            byte[]? rowVersion,
            CancellationToken cancellationToken)
        {
            if (expense.Id == 0)
            {
                _context.Expenses.Add(expense);
            }
            else
            {
                _context.Entry(expense)
                    .Property(row => row.RowVersion)
                    .OriginalValue = rowVersion!;
            }

            await _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}
