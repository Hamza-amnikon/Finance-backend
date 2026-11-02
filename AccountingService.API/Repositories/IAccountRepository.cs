using AccountingService.API.DTOs;
using AccountingService.API.Models;

namespace AccountingService.API.Repositories
{
    public interface IAccountRepository
    {
        Task<(IReadOnlyList<Account> Accounts, int Total)>
            GetAllAsync(
                AccountQueryDto query,
                CancellationToken cancellationToken);

        Task<Account?> GetByIdAsync(
            int id,
            int companyId,
            CancellationToken cancellationToken);

        Task<bool> CompanyExistsAsync(
            int companyId,
            CancellationToken cancellationToken);

        Task<bool> CodeExistsAsync(
            int companyId,
            string code,
            int? excludeAccountId,
            CancellationToken cancellationToken);

        Task<bool> HasChildrenAsync(
            int accountId,
            int companyId,
            CancellationToken cancellationToken);

        Task<bool> WouldCreateCycleAsync(
            int accountId,
            int parentAccountId,
            int companyId,
            CancellationToken cancellationToken);

        Task<List<AccountingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken);

        Task AddAsync(
            Account account,
            CancellationToken cancellationToken);

        Task SaveChangesAsync(
            CancellationToken cancellationToken);

        Task<IReadOnlyList<Account>> GetTreeAsync(
            int companyId,
            CancellationToken cancellationToken);
    }
}
