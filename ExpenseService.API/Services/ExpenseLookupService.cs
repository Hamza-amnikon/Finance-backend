using System.ComponentModel.DataAnnotations;
using ExpenseService.API.Models;
using ExpenseService.API.Repositories;

namespace ExpenseService.API.Services
{
    public class ExpenseLookupService : IExpenseLookupService
    {
        private readonly IExpenseLookupRepository _repository;

        public ExpenseLookupService(
            IExpenseLookupRepository repository)
        {
            _repository = repository;
        }

        public Task<List<ExpenseCountryLookup>> GetCountriesAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetCountriesAsync(cancellationToken);
        }

        public Task<List<ExpenseCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetCurrenciesAsync(cancellationToken);
        }

        public async Task<ExpenseCountryLookup> ResolveCountryAsync(
            string code,
            CancellationToken cancellationToken)
        {
            var countries = await _repository.GetCountriesAsync(cancellationToken);
            var matches = countries
                .Where(country => country.LocationCode == code)
                .ToList();

            if (matches.Count != 1)
            {
                throw new ValidationException(
                    "Select a country with one active currency mapping in the country master.");
            }

            return matches[0];
        }

        public async Task<ExpenseCurrencyLookup> ResolveCurrencyAsync(
            string? code,
            bool requireActive,
            CancellationToken cancellationToken)
        {
            var currency = string.IsNullOrWhiteSpace(code)
                ? (await _repository.GetCurrenciesAsync(cancellationToken)).FirstOrDefault()
                : await _repository.GetCurrencyAsync(code, cancellationToken);

            if (
                currency == null ||
                (requireActive && !currency.IsActive) ||
                currency.DecimalPlaces < 0 ||
                currency.DecimalPlaces > 4)
            {
                throw new ValidationException(
                    "Select a currency configured in the currency master.");
            }

            return currency;
        }
    }
}
