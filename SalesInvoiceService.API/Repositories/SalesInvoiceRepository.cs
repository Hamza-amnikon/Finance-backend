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

            var branch = await _context.BranchMasters
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    entry => entry.Name == invoice.Branch && entry.IsActive,
                    cancellationToken)
                ?? throw new ArgumentException("Select an active Billing Branch before issuing.");

            var prefix = branch.InvoiceNumberPrefix.Trim();

            if (prefix.Length > 10 || prefix.Any(character => !char.IsAsciiLetterOrDigit(character)))
            {
                throw new ArgumentException("Review the invoice number prefix in branch invoice details.");
            }

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
            var number = $"{invoice.InvoiceDate.Year:D4}/{invoice.InvoiceDate.Month:D3}/{counter.LastNumber:D3}";

            invoice.InvoiceNumber = string.IsNullOrEmpty(prefix)
                ? number
                : $"{prefix}-{number}";

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
                CancellationToken cancellationToken,
                string? branch = null)
        {
            var query = _context.SalesInvoices.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(branch))
            {
                var billingBranch = branch.Trim();
                query = query.Where(invoice => invoice.Branch == billingBranch);
            }

            var total = await query.CountAsync(cancellationToken);

            var offset = ((long)page - 1) * pageSize;

            if (offset >= total)
            {
                return ([], total);
            }

            var invoices = await query
                .OrderByDescending(invoice => invoice.CreatedDate)
                .ThenByDescending(invoice => invoice.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .Include(invoice => invoice.Items)
                .ToListAsync(cancellationToken);

            return (invoices, total);
        }
    }
}
