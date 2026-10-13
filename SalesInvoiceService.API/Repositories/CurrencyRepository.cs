using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public class CurrencyRepository : ICurrencyRepository
    {
        private readonly SalesInvoiceDbContext _context;

        public CurrencyRepository(SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public async Task<List<CurrencyMaster>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return await _context.CurrencyMasters
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.CurrencyCode)
                .ToListAsync(cancellationToken);
        }

        public async Task<CurrencyMaster?> GetByCodeAsync(
            string currencyCode,
            CancellationToken cancellationToken)
        {
            var code = currencyCode.Trim().ToUpperInvariant();

            return await _context.CurrencyMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.CurrencyCode == code,
                    cancellationToken);
        }
    }
}