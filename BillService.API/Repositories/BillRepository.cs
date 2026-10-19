using System.Data;
using Microsoft.EntityFrameworkCore;
using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;

namespace BillService.API.Repositories
{
    public class BillRepository : IBillRepository
    {
        private readonly BillDbContext _context;

        public BillRepository(
            BillDbContext db)
        {
            _context = db;
        }

        public Task<Bill?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
            _context.Bills.AsNoTracking().Include(row => row.Items.OrderBy(item => item.LineNumber))
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        public Task<Bill?> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) =>
            _context.Bills.AsNoTracking().Include(row => row.Items.OrderBy(item => item.LineNumber))
                .SingleOrDefaultAsync(row => row.RequestId == requestId, cancellationToken);
        public async Task<(List<Bill> Data, int Total)> GetPagedAsync(BillQueryDto query, CancellationToken cancellationToken)
        {
            var rows = _context.Bills.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                rows = rows.Where(row => row.VendorName.Contains(search) || row.CompanyName.Contains(search) ||
                    (row.BillNumber != null && row.BillNumber.Contains(search)) || row.Reference.Contains(search));
            }
            if (query.Status.HasValue) rows = rows.Where(row => row.Status == query.Status.Value);
            var total = await rows.CountAsync(cancellationToken);
            var offset = ((long)query.Page - 1) * query.PageSize;
            if (offset >= total) return ([], total);
            return (await rows.OrderByDescending(row => row.CreatedDate).ThenByDescending(row => row.Id)
                .Skip((int)offset).Take(query.PageSize).ToListAsync(cancellationToken), total);
        }
        public async Task<Bill> CreateAsync(Bill bill, CancellationToken cancellationToken)
        {
            _context.Bills.Add(bill);
            await _context.SaveChangesAsync(cancellationToken);
            return bill;
        }
        private Task<Bill?> LockAsync(int id, CancellationToken cancellationToken) =>
            _context.Bills.FromSqlInterpolated(
                $"SELECT * FROM [purchases].[Bills] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}")
                .Include(row => row.Items).SingleOrDefaultAsync(cancellationToken);

        public async Task<Bill?> UpdateDraftAsync(Bill bill, byte[] rowVersion, CancellationToken cancellationToken)
        {
            // Participate in a caller-owned unit of work when one exists.
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var stored = await LockAsync(bill.Id, cancellationToken);
            if (stored == null) return null;
            if (stored.Status != BillStatus.Draft)
                throw new InvalidOperationException("Only draft bills can be edited.");
            if (!stored.RowVersion.SequenceEqual(rowVersion)) throw new DbUpdateConcurrencyException();
            if (stored.SourcePurchaseOrderId.HasValue && (stored.VendorId != bill.VendorId || stored.Currency != bill.Currency || stored.Branch != bill.Branch))
                throw new InvalidOperationException("A linked bill must retain the purchase order vendor, branch and currency.");
            foreach (var name in new[] {"VendorInvoiceNumber","VendorId","VendorName","CompanyName","BillingAddress","ShippingAddress","BillDate",
                "DueDate","Reference","PaymentTerms","Notes","AdditionalTerms","Branch","Currency",
                "CurrencyDecimalPlaces","SubTotal","DiscountTotal","TaxTotal","GrandTotal"})
                _context.Entry(stored).Property(name).CurrentValue = _context.Entry(bill).Property(name).CurrentValue;
            stored.ModifiedDate = DateTime.UtcNow;
            _context.Entry(stored).Property(row => row.RowVersion).OriginalValue = rowVersion;
            _context.Entry(stored).Property(row => row.ModifiedDate).IsModified = true;
            await _context.SaveChangesAsync(cancellationToken);
            _context.BillItems.RemoveRange(stored.Items);
            await _context.SaveChangesAsync(cancellationToken);
            stored.Items.Clear();
            foreach (var item in bill.Items) stored.Items.Add(item);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return stored;
        }
        public async Task<Bill?> ChangeStatusAsync(int id, BillStatus target, byte[] rowVersion,
            Func<Bill, CancellationToken, Task> validateIssue, CancellationToken cancellationToken)
        {
            // Participate in a caller-owned unit of work when one exists.
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var stored = await LockAsync(id, cancellationToken);
            if (stored == null) return null;
            if (stored.Status == target) { if (transaction != null) await transaction.CommitAsync(cancellationToken); return stored; }
            if (!stored.RowVersion.SequenceEqual(rowVersion)) throw new DbUpdateConcurrencyException();
            if (target == BillStatus.Issued)
            {
                if (stored.Status != BillStatus.Draft)
                    throw new InvalidOperationException("Only drafts can be issued.");
                await validateIssue(stored, cancellationToken);
                var period = stored.BillDate.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
                var counter = await _context.NumberCounters.FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[BillNumberCounters] WITH (UPDLOCK,HOLDLOCK) WHERE [PeriodKey] = {period}")
                    .SingleOrDefaultAsync(cancellationToken);
                if (counter == null) { counter = new() { PeriodKey = period }; _context.NumberCounters.Add(counter); }
                counter.LastNumber = checked(counter.LastNumber + 1);
                stored.BillNumber = $"BILL/{stored.BillDate.Year:D4}/{stored.BillDate.Month:D3}/{counter.LastNumber:D3}";
                stored.IssuedDate = DateTime.UtcNow;
            }
            else if (target == BillStatus.Cancelled)
            {
                if (stored.Status != BillStatus.Draft && stored.Status != BillStatus.Issued)
                    throw new InvalidOperationException("This bill cannot be cancelled.");
                stored.CancelledDate = DateTime.UtcNow;
            }
            else throw new ArgumentException("Unsupported bill action.");
            stored.Status = target;
            stored.ModifiedDate = DateTime.UtcNow;
            _context.Entry(stored).Property(row => row.RowVersion).OriginalValue = rowVersion;
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return stored;
        }
    }
}
