using ExpenseService.API.Data;
using ExpenseService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Repositories
{
    public class ExpenseLookupRepository : IExpenseLookupRepository
    {
        private readonly ExpenseDbContext _context;

        public ExpenseLookupRepository(
            ExpenseDbContext context)
        {
            _context = context;
        }

        public Task<List<ExpenseCountryLookup>> GetCountriesAsync(
            CancellationToken cancellationToken)
        {
            return _context.Countries
                .AsNoTracking()
                .Where(country =>
                    country.LocationCode.Length == 2 &&
                    country.CountryName != "" &&
                    _context.Currencies.Any(currency =>
                        currency.IsActive &&
                        currency.CurrencyCode == country.CurrencyCode))
                .Select(country => new ExpenseCountryLookup
                {
                    CountryName = country.CountryName,
                    LocationCode = country.LocationCode,
                    CurrencyCode = country.CurrencyCode
                })
                .Distinct()
                .OrderBy(country => country.CountryName)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ExpenseCurrencyLookup>> GetCurrenciesAsync(
            CancellationToken cancellationToken)
        {
            return _context.Currencies
                .AsNoTracking()
                .Where(currency => currency.IsActive)
                .OrderBy(currency => currency.CurrencyCode)
                .ToListAsync(cancellationToken);
        }

        public Task<ExpenseCurrencyLookup?> GetCurrencyAsync(
            string code,
            CancellationToken cancellationToken)
        {
            return _context.Currencies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    currency => currency.CurrencyCode == code,
                    cancellationToken);
        }
    }
}
