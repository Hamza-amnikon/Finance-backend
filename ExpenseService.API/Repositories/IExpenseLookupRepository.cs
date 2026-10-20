using ExpenseService.API.Models;

namespace ExpenseService.API.Repositories
{
    public interface IExpenseLookupRepository
    {
        Task<List<ExpenseCountryLookup>> GetCountriesAsync(
            CancellationToken cancellationToken);

        Task<List<ExpenseCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken);

        Task<ExpenseCurrencyLookup?> GetCurrencyAsync(
            string code,
            CancellationToken cancellationToken);
    }
}
