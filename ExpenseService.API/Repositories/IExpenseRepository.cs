using ExpenseService.API.DTOs;
using ExpenseService.API.Models;

namespace ExpenseService.API.Repositories
{
    public interface IExpenseRepository
    {
        IQueryable<Expense> Query(
            ExpenseQueryDto query);

        Task<Expense?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<Expense?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<Company?> GetCompanyAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankAccount?> GetAccountAsync(
            int id,
            CancellationToken cancellationToken);

        Task SaveAsync(
            Expense expense,
            byte[]? rowVersion,
            CancellationToken cancellationToken);
    }
}
