using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public class SalesInvoiceRepository : ISalesInvoiceRepository
    {
        private readonly SalesInvoiceDbContext _context;

        public SalesInvoiceRepository(
            SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public async Task<SalesInvoice> CreateAsync(
            SalesInvoice invoice,
            CancellationToken cancellationToken)
        {
            _context.SalesInvoices.Add(invoice);

            await _context.SaveChangesAsync(cancellationToken);

            return invoice;
        }

        public async Task<SalesInvoice?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }
    }
}