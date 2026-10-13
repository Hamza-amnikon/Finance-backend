using ClientService.API.Data;
using Microsoft.EntityFrameworkCore;

namespace ClientService.API.Repositories
{
    public class CountryMasterRepository : ICountryMasterRepository
    {
        private readonly ClientDbContext _context;

        public CountryMasterRepository(ClientDbContext context)
        {
            _context = context;
        }

        public Task<List<string>> GetCountriesAsync(
            CancellationToken cancellationToken)
        {
            return _context.CountryMasters
                .AsNoTracking()
                .Where(x => x.CountryName != "")
                .Select(x => x.CountryName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        public Task<List<string>> GetStatesAsync(
            string country,
            CancellationToken cancellationToken)
        {
            return _context.CountryMasters
                .AsNoTracking()
                .Where(x =>
                    x.CountryName == country &&
                    x.State != "")
                .Select(x => x.State)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        public Task<List<string>> GetCitiesAsync(
            string country,
            string? state,
            CancellationToken cancellationToken)
        {
            var query = _context.CountryMasters
                .AsNoTracking()
                .Where(x =>
                    x.CountryName == country &&
                    x.City != "");

            if (!string.IsNullOrWhiteSpace(state))
            {
                query = query.Where(x => x.State == state);
            }

            return query
                .Select(x => x.City)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }
    }
}