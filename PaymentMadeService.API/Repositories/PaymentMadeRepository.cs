using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PaymentMadeService.API.Data;
using PaymentMadeService.API.DTOs;
using PaymentMadeService.API.Models;

namespace PaymentMadeService.API.Repositories
{
    public class PaymentMadeRepository : IPaymentMadeRepository
    {
        private readonly PaymentMadeDbContext _context;

        public PaymentMadeRepository(PaymentMadeDbContext context)
        {
            _context = context;
        }

        public async Task<(List<PaymentMade> Data, int Total)> GetPagedAsync(
            PaymentMadeQueryDto query,
            CancellationToken cancellationToken)
        {
            var rows = _context.PaymentsMade.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                rows = rows.Where(row =>
                    row.PaymentNumber.Contains(search) ||
                    row.VendorName.Contains(search) ||
                    row.BillNumber.Contains(search) ||
                    row.Reference.Contains(search));
            }

            if (query.Status.HasValue)
            {
                rows = rows.Where(row => row.Status == query.Status.Value);
            }

            if (query.VendorId.HasValue)
            {
                rows = rows.Where(row => row.VendorId == query.VendorId.Value);
            }

            if (query.BillId.HasValue)
            {
                rows = rows.Where(row => row.BillId == query.BillId.Value);
            }

            if (query.FromDate.HasValue)
            {
                rows = rows.Where(row => row.PaymentDate >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                rows = rows.Where(row => row.PaymentDate <= query.ToDate.Value);
            }

            var total = await rows.CountAsync(cancellationToken);
            var offset = ((long)query.Page - 1) * query.PageSize;

            if (offset >= total)
            {
                return ([], total);
            }

            var data = await rows
                .OrderByDescending(row => row.PaymentDate)
                .ThenByDescending(row => row.Id)
                .Skip((int)offset)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return (data, total);
        }

        public Task<PaymentMade?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.PaymentsMade
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        }

        public Task<PaymentMade?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return _context.PaymentsMade
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.RequestId == requestId,
                    cancellationToken);
        }

        private Task<PaymentBillLookup?> LockBillAsync(
            int billId,
            CancellationToken cancellationToken)
        {
            return _context.Bills
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[Bills] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {billId}")
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<PaymentMade> RecordAsync(
            PaymentMade payment,
            byte[] billRowVersion,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

            // Payment creation, voiding and bill cancellation lock the bill first.
            var bill = await LockBillAsync(payment.BillId, cancellationToken);

            if (bill == null)
            {
                throw new ArgumentException("The selected bill was not found.");
            }

            var existing = await GetByRequestIdAsync(
                payment.RequestId,
                cancellationToken);

            if (existing != null)
            {
                if (existing.RequestFingerprint != payment.RequestFingerprint)
                {
                    throw new InvalidOperationException(
                        "This request ID was already used for different payment details.");
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return existing;
            }

            if (bill.Status != "Issued")
            {
                throw new InvalidOperationException(
                    "Payments can only be recorded against issued bills.");
            }

            if (!bill.RowVersion.SequenceEqual(billRowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            if (
                bill.VendorId != payment.VendorId ||
                bill.Currency != payment.Currency ||
                bill.CurrencyDecimalPlaces != payment.CurrencyDecimalPlaces)
            {
                throw new InvalidOperationException(
                    "The bill settings changed. Refresh the bill before paying.");
            }

            var vendorActive = await _context.Vendors.AnyAsync(
                row => row.Id == payment.VendorId && row.IsActive,
                cancellationToken);

            var modeActive = await _context.PaymentModes.AnyAsync(
                row => row.Id == payment.PaymentModeId && row.IsActive,
                cancellationToken);

            if (!vendorActive || !modeActive)
            {
                throw new ArgumentException(
                    "Select an active vendor and payment mode.");
            }

            var paid = await _context.PaymentsMade
                .Where(row =>
                    row.BillId == bill.Id &&
                    row.Status == PaymentMadeStatus.Recorded)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            var appliedCredit = await _context.CreditApplications
                .Where(row => row.BillId == bill.Id && row.Status == "Applied")
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            if (payment.Amount > bill.GrandTotal - paid - appliedCredit)
            {
                throw new InvalidOperationException(
                    "Payment exceeds the current outstanding balance. Refresh the bill and try again.");
            }

            var period = payment.PaymentDate.ToString(
                "yyyyMM",
                CultureInfo.InvariantCulture);

            var counter = await _context.NumberCounters
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[PaymentMadeNumberCounters] WITH (UPDLOCK,HOLDLOCK) WHERE [PeriodKey] = {period}")
                .SingleOrDefaultAsync(cancellationToken);

            if (counter == null)
            {
                counter = new PaymentMadeNumberCounter
                {
                    PeriodKey = period
                };

                _context.NumberCounters.Add(counter);
            }

            counter.LastNumber = checked(counter.LastNumber + 1);

            payment.PaymentNumber =
                $"PM/{payment.PaymentDate.Year:D4}/{payment.PaymentDate.Month:D3}/{counter.LastNumber:D3}";

            _context.PaymentsMade.Add(payment);

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return payment;
        }

        public async Task<PaymentMade?> VoidAsync(
            int id,
            VoidPaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            var snapshot = await GetByIdAsync(id, cancellationToken);

            if (snapshot == null)
            {
                return null;
            }

            await using var transaction =
                _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

            await LockBillAsync(snapshot.BillId, cancellationToken);

            var payment = await _context.PaymentsMade
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[PaymentsMade] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}")
                .SingleAsync(cancellationToken);

            if (payment.Status == PaymentMadeStatus.Voided)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return payment;
            }

            if (!payment.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            payment.Status = PaymentMadeStatus.Voided;
            payment.VoidedDate = DateTime.UtcNow;
            payment.VoidReason = dto.Reason.Trim();

            _context.Entry(payment)
                .Property(row => row.RowVersion)
                .OriginalValue = dto.RowVersion;

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return payment;
        }
    }
}
