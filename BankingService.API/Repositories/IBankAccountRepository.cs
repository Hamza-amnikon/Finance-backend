using BankingService.API.Models;

namespace BankingService.API.Repositories
{
    public interface IBankAccountRepository
    {
        IQueryable<BankAccount> Query();

        Task<BankAccount?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankingCompanyLookup?> GetCompanyAsync(
            int companyId,
            CancellationToken cancellationToken);

        Task<BankingCurrencyLookup?> GetCurrencyAsync(
            string currencyCode,
            CancellationToken cancellationToken);

        Task<List<BankingCompanyLookup>> GetCompaniesAsync(
            CancellationToken cancellationToken);

        Task<List<BankingCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken);

        Task AddAsync(
            BankAccount account,
            CancellationToken cancellationToken);

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}