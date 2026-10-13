using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public class PaymentModeRepository : IPaymentModeRepository
    {
        private readonly SalesInvoiceDbContext _context;

        public PaymentModeRepository(
            SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public async Task<List<PaymentMode>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return await _context.PaymentModes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<PaymentMode?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            var value = name.Trim();

            return await _context.PaymentModes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Name == value && x.IsActive,
                    cancellationToken);
        }
    }
}