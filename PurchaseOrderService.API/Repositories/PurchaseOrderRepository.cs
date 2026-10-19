using System.Data;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.Repositories;

public class PurchaseOrderRepository(PurchaseOrderDbContext db) : IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetByIdAsync(int id, CancellationToken ct) =>
        db.PurchaseOrders.AsNoTracking().Include(row => row.Items.OrderBy(item => item.LineNumber))
            .SingleOrDefaultAsync(row => row.Id == id, ct);
    public Task<PurchaseOrder?> GetByRequestIdAsync(Guid requestId, CancellationToken ct) =>
        db.PurchaseOrders.AsNoTracking().Include(row => row.Items.OrderBy(item => item.LineNumber))
            .SingleOrDefaultAsync(row => row.RequestId == requestId, ct);
    public async Task<(List<PurchaseOrder> Data, int Total)> GetPagedAsync(PurchaseOrderQueryDto query, CancellationToken ct)
    {
        var rows = db.PurchaseOrders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(row => row.VendorName.Contains(search) || row.CompanyName.Contains(search) ||
                (row.PurchaseOrderNumber != null && row.PurchaseOrderNumber.Contains(search)) || row.Reference.Contains(search));
        }
        if (query.Status.HasValue) rows = rows.Where(row => row.Status == query.Status.Value);
        var total = await rows.CountAsync(ct);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= total) return ([], total);
        return (await rows.OrderByDescending(row => row.CreatedDate).ThenByDescending(row => row.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct), total);
    }
    public async Task<PurchaseOrder> CreateAsync(PurchaseOrder order, CancellationToken ct)
    {
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }
    private Task<PurchaseOrder?> LockAsync(int id, CancellationToken ct) =>
        db.PurchaseOrders.FromSqlInterpolated(
            $"SELECT * FROM [purchases].[PurchaseOrders] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}")
            .Include(row => row.Items).SingleOrDefaultAsync(ct);

    public async Task<PurchaseOrder?> UpdateDraftAsync(PurchaseOrder order, byte[] rowVersion, CancellationToken ct)
    {
        // Participate in a caller-owned unit of work when one exists.
        await using var transaction = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var stored = await LockAsync(order.Id, ct);
        if (stored == null) return null;
        if (stored.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Only draft purchase orders can be edited.");
        if (!stored.RowVersion.SequenceEqual(rowVersion)) throw new DbUpdateConcurrencyException();
        foreach (var name in new[] {"VendorId","VendorName","CompanyName","BillingAddress","ShippingAddress","OrderDate",
            "ExpectedDeliveryDate","Reference","PaymentTerms","Notes","AdditionalTerms","Branch","Currency",
            "CurrencyDecimalPlaces","SubTotal","DiscountTotal","TaxTotal","GrandTotal"})
            db.Entry(stored).Property(name).CurrentValue = db.Entry(order).Property(name).CurrentValue;
        stored.ModifiedDate = DateTime.UtcNow;
        db.Entry(stored).Property(row => row.RowVersion).OriginalValue = rowVersion;
        db.Entry(stored).Property(row => row.ModifiedDate).IsModified = true;
        await db.SaveChangesAsync(ct);
        db.PurchaseOrderItems.RemoveRange(stored.Items);
        await db.SaveChangesAsync(ct);
        stored.Items.Clear();
        foreach (var item in order.Items) stored.Items.Add(item);
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return stored;
    }
    public async Task<PurchaseOrder?> ChangeStatusAsync(int id, PurchaseOrderStatus target, byte[] rowVersion,
        Func<PurchaseOrder, CancellationToken, Task> validateIssue, CancellationToken ct)
    {
        // Participate in a caller-owned unit of work when one exists.
        await using var transaction = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var stored = await LockAsync(id, ct);
        if (stored == null) return null;
        if (stored.Status == target) { if (transaction != null) await transaction.CommitAsync(ct); return stored; }
        if (!stored.RowVersion.SequenceEqual(rowVersion)) throw new DbUpdateConcurrencyException();
        if (target == PurchaseOrderStatus.Issued)
        {
            if (stored.Status != PurchaseOrderStatus.Draft)
                throw new InvalidOperationException("Only drafts can be issued.");
            await validateIssue(stored, ct);
            var period = stored.OrderDate.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
            var counter = await db.NumberCounters.FromSqlInterpolated(
                $"SELECT * FROM [purchases].[PurchaseOrderNumberCounters] WITH (UPDLOCK,HOLDLOCK) WHERE [PeriodKey] = {period}")
                .SingleOrDefaultAsync(ct);
            if (counter == null) { counter = new() { PeriodKey = period }; db.NumberCounters.Add(counter); }
            counter.LastNumber = checked(counter.LastNumber + 1);
            stored.PurchaseOrderNumber = $"PO/{stored.OrderDate.Year:D4}/{stored.OrderDate.Month:D3}/{counter.LastNumber:D3}";
            stored.IssuedDate = DateTime.UtcNow;
        }
        else if (target == PurchaseOrderStatus.Cancelled)
        {
            if (stored.Status != PurchaseOrderStatus.Draft && stored.Status != PurchaseOrderStatus.Issued)
                throw new InvalidOperationException("This order cannot be cancelled.");
            stored.CancelledDate = DateTime.UtcNow;
        }
        else throw new ArgumentException("Unsupported purchase order action.");
        stored.Status = target;
        stored.ModifiedDate = DateTime.UtcNow;
        db.Entry(stored).Property(row => row.RowVersion).OriginalValue = rowVersion;
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return stored;
    }
}
