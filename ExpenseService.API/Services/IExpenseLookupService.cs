using ExpenseService.API.Models;

namespace ExpenseService.API.Services
{
    public interface IExpenseLookupService
    {
        Task<List<ExpenseCountryLookup>> GetCountriesAsync(
            CancellationToken cancellationToken);

        Task<List<ExpenseCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken);

        Task<ExpenseCountryLookup> ResolveCountryAsync(
            string code,
            CancellationToken cancellationToken);

        Task<ExpenseCurrencyLookup> ResolveCurrencyAsync(
            string? code,
            bool requireActive,
            CancellationToken cancellationToken);
    }
}
