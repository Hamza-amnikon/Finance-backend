using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VendorCreditService.API.Data;
using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.Repositories
{
    public class VendorCreditRepository : IVendorCreditRepository
    {
        private readonly VendorCreditDbContext _context;

        public VendorCreditRepository(
            VendorCreditDbContext context)
        {
            _context = context;
        }

        public async Task<(
            List<VendorCredit> Data,
            int Total)> GetPagedAsync(
                VendorCreditQueryDto query,
                CancellationToken cancellationToken)
        {
            var rows = _context.VendorCredits
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                rows = rows.Where(row =>
                    row.VendorName.Contains(search) ||
                    row.CompanyName.Contains(search) ||
                    row.VendorCreditNoteNumber.Contains(search) ||
                    row.Reference.Contains(search) ||
                    (
                        row.VendorCreditNumber != null &&
                        row.VendorCreditNumber.Contains(search)
                    ));
            }

            if (query.Status.HasValue)
            {
                rows = rows.Where(row =>
                    row.Status == query.Status.Value);
            }

            if (query.VendorId.HasValue)
            {
                rows = rows.Where(row =>
                    row.VendorId == query.VendorId.Value);
            }

            if (query.FromDate.HasValue)
            {
                rows = rows.Where(row =>
                    row.CreditDate >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                rows = rows.Where(row =>
                    row.CreditDate <= query.ToDate.Value);
            }

            var total = await rows.CountAsync(
                cancellationToken);

            var offset = ((long)query.Page - 1) * query.PageSize;

            if (offset >= total)
            {
                return ([], total);
            }

            var data = await rows
                .OrderByDescending(row => row.CreatedDate)
                .ThenByDescending(row => row.Id)
                .Skip((int)offset)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return (data, total);
        }

        public Task<VendorCredit?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.VendorCredits
                .AsNoTracking()
                .Include(row =>
                    row.Items.OrderBy(item => item.LineNumber))
                .SingleOrDefaultAsync(
                    row => row.Id == id,
                    cancellationToken);
        }

        public Task<VendorCredit?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return _context.VendorCredits
                .AsNoTracking()
                .Include(row =>
                    row.Items.OrderBy(item => item.LineNumber))
                .SingleOrDefaultAsync(
                    row => row.RequestId == requestId,
                    cancellationToken);
        }

        public async Task<VendorCredit> CreateAsync(
            VendorCredit credit,
            CancellationToken cancellationToken)
        {
            _context.VendorCredits.Add(credit);

            await _context.SaveChangesAsync(
                cancellationToken);

            return credit;
        }

        private Task<VendorCredit?> LockAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.VendorCredits
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[VendorCredits] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}")
                .Include(row => row.Items)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<VendorCredit?> UpdateDraftAsync(
            VendorCredit credit,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

            var stored = await LockAsync(
                credit.Id,
                cancellationToken);

            if (stored == null)
            {
                return null;
            }

            if (stored.Status != VendorCreditStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft vendor credits can be edited.");
            }

            if (!stored.RowVersion.SequenceEqual(rowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            foreach (var name in new[]
            {
                nameof(VendorCredit.VendorId),
                nameof(VendorCredit.BillId),
                nameof(VendorCredit.VendorCreditNoteNumber),
                nameof(VendorCredit.VendorName),
                nameof(VendorCredit.CompanyName),
                nameof(VendorCredit.BillingAddress),
                nameof(VendorCredit.CreditDate),
                nameof(VendorCredit.Reference),
                nameof(VendorCredit.Reason),
                nameof(VendorCredit.Notes),
                nameof(VendorCredit.Branch),
                nameof(VendorCredit.Currency),
                nameof(VendorCredit.CurrencyDecimalPlaces),
                nameof(VendorCredit.SubTotal),
                nameof(VendorCredit.DiscountTotal),
                nameof(VendorCredit.TaxTotal),
                nameof(VendorCredit.GrandTotal)
            })
            {
                _context.Entry(stored)
                    .Property(name)
                    .CurrentValue = _context.Entry(credit)
                        .Property(name)
                        .CurrentValue;
            }

            stored.ModifiedDate = DateTime.UtcNow;

            _context.Entry(stored)
                .Property(row => row.RowVersion)
                .OriginalValue = rowVersion;

            _context.Entry(stored)
                .Property(row => row.ModifiedDate)
                .IsModified = true;

            await _context.SaveChangesAsync(
                cancellationToken);

            _context.VendorCreditItems.RemoveRange(
                stored.Items);

            await _context.SaveChangesAsync(
                cancellationToken);

            stored.Items.Clear();

            foreach (var item in credit.Items)
            {
                stored.Items.Add(item);
            }

            await _context.SaveChangesAsync(
                cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }

            return stored;
        }

        public async Task<VendorCredit?> ChangeStatusAsync(
            int id,
            VendorCreditStatus target,
            byte[] rowVersion,
            Func<VendorCredit, CancellationToken, Task> validateIssue,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

            var stored = await LockAsync(
                id,
                cancellationToken);

            if (stored == null)
            {
                return null;
            }

            if (stored.Status == target)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync(
                        cancellationToken);
                }

                return stored;
            }

            if (!stored.RowVersion.SequenceEqual(rowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            if (target == VendorCreditStatus.Issued)
            {
                if (stored.Status != VendorCreditStatus.Draft)
                {
                    throw new InvalidOperationException(
                        "Only draft vendor credits can be issued.");
                }

                await validateIssue(
                    stored,
                    cancellationToken);

                var period = stored.CreditDate.ToString(
                    "yyyyMM",
                    CultureInfo.InvariantCulture);

                var counter = await _context.NumberCounters
                    .FromSqlInterpolated(
                        $"SELECT * FROM [purchases].[VendorCreditNumberCounters] WITH (UPDLOCK,HOLDLOCK) WHERE [PeriodKey] = {period}")
                    .SingleOrDefaultAsync(cancellationToken);

                if (counter == null)
                {
                    counter = new VendorCreditNumberCounter
                    {
                        PeriodKey = period
                    };

                    _context.NumberCounters.Add(counter);
                }

                counter.LastNumber = checked(
                    counter.LastNumber + 1);

                stored.VendorCreditNumber =
                    $"VC/{stored.CreditDate.Year:D4}/{stored.CreditDate.Month:D3}/{counter.LastNumber:D3}";

                stored.IssuedDate = DateTime.UtcNow;
            }
            else if (target == VendorCreditStatus.Cancelled)
            {
                if (
                    stored.Status != VendorCreditStatus.Draft &&
                    stored.Status != VendorCreditStatus.Issued)
                {
                    throw new InvalidOperationException(
                        "This vendor credit cannot be cancelled.");
                }

                var hasApplications = await _context.Applications.AnyAsync(
                    row =>
                        row.VendorCreditId == stored.Id &&
                        row.Status == CreditApplicationStatus.Applied,
                    cancellationToken);

                if (hasApplications)
                {
                    throw new InvalidOperationException(
                        "Void active credit applications before cancelling this vendor credit.");
                }

                stored.CancelledDate = DateTime.UtcNow;
            }
            else
            {
                throw new ArgumentException(
                    "Unsupported vendor credit action.");
            }

            stored.Status = target;
            stored.ModifiedDate = DateTime.UtcNow;

            _context.Entry(stored)
                .Property(row => row.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(
                cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }

            return stored;
        }
    }
}
