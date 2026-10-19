using System.Data;
using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.Data;
using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.Repositories
{
    public class SalesEstimateRepository : ISalesEstimateRepository
    {
        private readonly SalesEstimateDbContext _context;

        public SalesEstimateRepository(
            SalesEstimateDbContext context)
        {
            _context = context;
        }

        public async Task<SalesEstimate> CreateAsync(
            SalesEstimate estimate,
            CancellationToken cancellationToken)
        {
            if (estimate.Status != SalesEstimateStatus.Draft)
            {
                throw new InvalidOperationException(
                    "A new estimate must be a draft.");
            }

            _context.SalesEstimates.Add(estimate);

            await _context.SaveChangesAsync(cancellationToken);

            return estimate;
        }

        public async Task<SalesEstimate?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.SalesEstimates
                .AsNoTracking()
                .Include(estimate => estimate.Items.OrderBy(item => item.LineNumber))
                .FirstOrDefaultAsync(
                    estimate => estimate.Id == id,
                    cancellationToken);
        }

        public async Task<(List<SalesEstimate> Data, int Total)>
            GetAllAsync(
                int page,
                int pageSize,
                string? search,
                SalesEstimateStatus? status,
                CancellationToken cancellationToken)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                throw new ArgumentException("Invalid estimate pagination.");
            }

            var query = _context.SalesEstimates.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();

                query = query.Where(estimate =>
                    (estimate.EstimateNumber != null &&
                        estimate.EstimateNumber.Contains(value)) ||
                    estimate.ClientName.Contains(value) ||
                    estimate.CompanyName.Contains(value));
            }

            if (status.HasValue)
            {
                query = query.Where(estimate => estimate.Status == status.Value);
            }

            var total = await query.CountAsync(cancellationToken);
            var skip = ((long)page - 1) * pageSize;

            if (skip >= total)
            {
                return (new List<SalesEstimate>(), total);
            }

            var estimates = await query
                .OrderByDescending(estimate => estimate.CreatedDate)
                .ThenByDescending(estimate => estimate.Id)
                .Skip((int)skip)
                .Take(pageSize)
                .Include(estimate => estimate.Items.OrderBy(item => item.LineNumber))
                .ToListAsync(cancellationToken);

            return (estimates, total);
        }

        public async Task<SalesEstimate?> CompleteConversionAsync(
            int id,
            int invoiceId,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            if (invoiceId < 1 || rowVersion == null || rowVersion.Length != 8)
                throw new ArgumentException("A valid invoice ID and RowVersion are required.");

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var estimate = await _context.SalesEstimates
                .FromSqlInterpolated($"SELECT * FROM [estimates].[SalesEstimates] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(cancellationToken);

            if (estimate == null)
                return null;

            if (estimate.Status == SalesEstimateStatus.Converted && estimate.ConvertedInvoiceId == invoiceId)
            {
                await transaction.CommitAsync(cancellationToken);
                return estimate;
            }

            if (estimate.Status != SalesEstimateStatus.Accepted)
                throw new InvalidOperationException("Only accepted estimates can be converted.");

            if (!estimate.RowVersion.SequenceEqual(rowVersion))
                throw new DbUpdateConcurrencyException();

            estimate.ConvertedInvoiceId = invoiceId;
            estimate.ConvertedDate = DateTime.UtcNow;
            estimate.ModifiedDate = estimate.ConvertedDate;
            estimate.Status = SalesEstimateStatus.Converted;

            _context.Entry(estimate).Property(record => record.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return estimate;
        }

        public async Task<SalesEstimate?> ChangeStatusAsync(
            int id,
            SalesEstimateStatus target,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            if (rowVersion == null || rowVersion.Length != 8)
            {
                throw new ArgumentException("A valid RowVersion is required.");
            }

            if (target != SalesEstimateStatus.Sent &&
                target != SalesEstimateStatus.Accepted &&
                target != SalesEstimateStatus.Declined)
            {
                throw new ArgumentException("Unsupported estimate action.");
            }

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var estimate = await _context.SalesEstimates
                .FromSqlInterpolated($"SELECT * FROM [estimates].[SalesEstimates] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(cancellationToken);

            if (estimate == null)
            {
                return null;
            }

            await _context.Entry(estimate).Collection(record => record.Items)
                .LoadAsync(cancellationToken);

            // A retry returns the completed action without a new number or timestamp.
            if (estimate.Status == target)
            {
                await transaction.CommitAsync(cancellationToken);
                return estimate;
            }

            var expected = target == SalesEstimateStatus.Sent
                ? SalesEstimateStatus.Draft
                : SalesEstimateStatus.Sent;

            if (estimate.Status != expected)
            {
                throw new InvalidOperationException(
                    "This action is not allowed for the current estimate status.");
            }

            if (!estimate.RowVersion.SequenceEqual(rowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            var now = DateTime.UtcNow;

            if (target == SalesEstimateStatus.Sent)
            {
                var periodKey = $"{estimate.EstimateDate.Year:D4}{estimate.EstimateDate.Month:D2}";
                var counter = await _context.Set<EstimateNumberCounter>()
                    .FromSqlInterpolated($"SELECT * FROM [estimates].[EstimateNumberCounters] WITH (UPDLOCK, HOLDLOCK) WHERE [PeriodKey] = {periodKey}")
                    .SingleOrDefaultAsync(cancellationToken);

                if (counter == null)
                {
                    counter = new EstimateNumberCounter { PeriodKey = periodKey };
                    _context.Add(counter);
                }

                counter.LastNumber = checked(counter.LastNumber + 1);
                estimate.EstimateNumber = $"EST/{estimate.EstimateDate.Year:D4}/{estimate.EstimateDate.Month:D3}/{counter.LastNumber:D3}";
                estimate.SentDate = now;
            }
            else if (target == SalesEstimateStatus.Accepted)
            {
                estimate.AcceptedDate = now;
            }
            else
            {
                estimate.DeclinedDate = now;
            }

            estimate.Status = target;
            estimate.ModifiedDate = now;

            _context.Entry(estimate).Property(record => record.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return estimate;
        }

        public async Task<SalesEstimate?> UpdateDraftAsync(
            SalesEstimate estimate,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            if (rowVersion == null || rowVersion.Length != 8)
            {
                throw new ArgumentException("A valid RowVersion is required.");
            }

            if (estimate.Status != SalesEstimateStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft estimates can be edited.");
            }

            if (estimate.Items == null || estimate.Items.Count == 0 ||
                estimate.Items.Any(item => item == null || item.Id != 0))
            {
                throw new ArgumentException(
                    "Provide replacement service lines without existing item IDs.");
            }

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            var stored = await _context.SalesEstimates
                .Include(record => record.Items)
                .FirstOrDefaultAsync(
                    record => record.Id == estimate.Id,
                    cancellationToken);

            if (stored == null)
            {
                return null;
            }

            if (stored.Status != SalesEstimateStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft estimates can be edited.");
            }

            stored.ClientId = estimate.ClientId;
            stored.ClientName = estimate.ClientName;
            stored.CompanyName = estimate.CompanyName;
            stored.BillingAddress = estimate.BillingAddress;
            stored.EstimateDate = estimate.EstimateDate;
            stored.ValidUntil = estimate.ValidUntil;
            stored.ServicePeriodStart = estimate.ServicePeriodStart;
            stored.ServicePeriodEnd = estimate.ServicePeriodEnd;
            stored.Currency = estimate.Currency;
            stored.Branch = estimate.Branch;
            stored.SubTotal = estimate.SubTotal;
            stored.DiscountTotal = estimate.DiscountTotal;
            stored.TaxTotal = estimate.TaxTotal;
            stored.GrandTotal = estimate.GrandTotal;
            stored.Notes = estimate.Notes;
            stored.AdditionalTerms = estimate.AdditionalTerms;
            stored.ModifiedDate = DateTime.UtcNow;

            _context.Entry(stored).Property(record => record.RowVersion)
                .OriginalValue = rowVersion;
            _context.Entry(stored).Property(record => record.ModifiedDate)
                .IsModified = true;

            // Check concurrency and lock the parent before replacing its lines.
            await _context.SaveChangesAsync(cancellationToken);

            // Delete first so replacement line numbers cannot collide.
            _context.SalesEstimateItems.RemoveRange(stored.Items);
            await _context.SaveChangesAsync(cancellationToken);
            stored.Items.Clear();

            foreach (var item in estimate.Items)
            {
                item.SalesEstimateId = stored.Id;
                stored.Items.Add(item);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return stored;
        }
    }
}
