using System.Data;
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

        public async Task<SalesInvoice> IssueAsync(
            SalesInvoice invoice,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var periodKey = $"{invoice.InvoiceDate.Year:D4}{invoice.InvoiceDate.Month:D2}";
            var counter = await _context.Set<InvoiceNumberCounter>()
                .FromSqlInterpolated($"SELECT * FROM [sales].[InvoiceNumberCounters] WITH (UPDLOCK, HOLDLOCK) WHERE [PeriodKey] = {periodKey}")
                .SingleOrDefaultAsync(cancellationToken);

            if (counter == null)
            {
                counter = new InvoiceNumberCounter { PeriodKey = periodKey };
                _context.Add(counter);
            }

            counter.LastNumber = checked(counter.LastNumber + 1);
            invoice.InvoiceNumber = $"{invoice.InvoiceDate.Year:D4}/{invoice.InvoiceDate.Month:D3}/{counter.LastNumber:D3}";

            _context.SalesInvoices.Attach(invoice);
            _context.Entry(invoice).State = EntityState.Modified;
            _context.Entry(invoice).Property(entry => entry.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return invoice;
        }

        public async Task<SalesInvoice?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(invoice => invoice.Items)
                .FirstOrDefaultAsync(
                    invoice => invoice.Id == id,
                    cancellationToken);
        }

        public async Task<(List<SalesInvoice> Data, int Total)>
            GetAllAsync(
                int page,
                int pageSize,
                CancellationToken cancellationToken)
        {
            var query = _context.SalesInvoices.AsNoTracking();

            var total = await query.CountAsync(cancellationToken);

            var invoices = await query
                .OrderByDescending(invoice => invoice.CreatedDate)
                .ThenByDescending(invoice => invoice.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(invoice => invoice.Items)
                .ToListAsync(cancellationToken);

            return (invoices, total);
        }
    }
}