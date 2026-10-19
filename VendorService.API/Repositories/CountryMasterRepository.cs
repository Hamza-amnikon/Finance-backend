using Microsoft.EntityFrameworkCore;
using VendorService.API.Data;

namespace VendorService.API.Repositories
{
    public class CountryMasterRepository : ICountryMasterRepository
    {
        private readonly VendorLocationDbContext _context;

        public CountryMasterRepository(VendorLocationDbContext context)
        {
            _context = context;
        }

        public Task<List<string>> GetCountriesAsync(CancellationToken cancellationToken)
        {
            return _context.CountryMasters
                .AsNoTracking()
                .Where(row => row.CountryName != "")
                .Select(row => row.CountryName)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken);
        }

        public Task<List<string>> GetCurrencyCodesAsync(
            string country, CancellationToken cancellationToken)
        {
            return _context.CountryMasters
                .AsNoTracking()
                .Where(row => row.CountryName == country && row.CurrencyCode != "")
                .Select(row => row.CurrencyCode.Trim().ToUpper())
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync(cancellationToken);
        }

        public Task<List<string>> GetStatesAsync(
            string country, CancellationToken cancellationToken)
        {
            return _context.CountryMasters
                .AsNoTracking()
                .Where(row => row.CountryName == country && row.State != "")
                .Select(row => row.State)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken);
        }

        public Task<List<string>> GetCitiesAsync(
            string country, string? state, CancellationToken cancellationToken)
        {
            var query = _context.CountryMasters
                .AsNoTracking()
                .Where(row => row.CountryName == country && row.City != "");

            if (!string.IsNullOrWhiteSpace(state))
            {
                query = query.Where(row => row.State == state);
            }

            return query
                .Select(row => row.City)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken);
        }
    }
}
