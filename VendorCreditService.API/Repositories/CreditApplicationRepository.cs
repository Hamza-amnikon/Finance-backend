using System.Data;
using Microsoft.EntityFrameworkCore;
using VendorCreditService.API.Data;
using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.Repositories
{
    public class CreditApplicationRepository : ICreditApplicationRepository
    {
        private readonly VendorCreditDbContext _context;

        public CreditApplicationRepository(VendorCreditDbContext context)
        {
            _context = context;
        }

        public Task<VendorCreditApplication?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return _context.Applications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.RequestId == requestId,
                    cancellationToken);
        }

        private Task<PurchaseBillLookup?> LockBillAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.Bills
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[Bills] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(cancellationToken);
        }

        private Task<VendorCredit?> LockCreditAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return _context.VendorCredits
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[VendorCredits] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<VendorCreditApplication> ApplyAsync(
            VendorCreditApplication application,
            byte[] creditRowVersion,
            byte[] billRowVersion,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

            // Use the same bill-first lock order as payments and bill cancellation.
            var bill = await LockBillAsync(application.BillId, cancellationToken)
                ?? throw new ArgumentException("The selected bill was not found.");

            var existing = await GetByRequestIdAsync(
                application.RequestId,
                cancellationToken);

            if (existing != null)
            {
                if (existing.RequestFingerprint != application.RequestFingerprint)
                {
                    throw new InvalidOperationException(
                        "This request ID was already used for different application details.");
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return existing;
            }

            var credit = await LockCreditAsync(
                application.VendorCreditId,
                cancellationToken)
                ?? throw new ArgumentException("Vendor credit not found.");

            if (credit.Status != VendorCreditStatus.Issued || bill.Status != "Issued")
            {
                throw new InvalidOperationException(
                    "Only issued vendor credits can be applied to issued bills.");
            }

            if (
                !credit.RowVersion.SequenceEqual(creditRowVersion) ||
                !bill.RowVersion.SequenceEqual(billRowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            if (
                credit.VendorId != bill.VendorId ||
                credit.Branch != bill.Branch ||
                credit.Currency != bill.Currency ||
                credit.CurrencyDecimalPlaces != bill.CurrencyDecimalPlaces)
            {
                throw new ArgumentException(
                    "The bill must match the credit vendor, branch and currency.");
            }

            if (
                credit.CurrencyDecimalPlaces < 0 ||
                credit.CurrencyDecimalPlaces > 4 ||
                decimal.Round(application.Amount, credit.CurrencyDecimalPlaces) != application.Amount)
            {
                throw new ArgumentException(
                    "Use the credit currency's decimal precision.");
            }

            if (
                application.ApplicationDate < credit.CreditDate ||
                application.ApplicationDate < bill.BillDate)
            {
                throw new ArgumentException(
                    "Application date cannot precede the credit date or bill date.");
            }

            var creditUsed = await _context.Applications
                .Where(row =>
                    row.VendorCreditId == credit.Id &&
                    row.Status == CreditApplicationStatus.Applied)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            var billCredits = await _context.Applications
                .Where(row =>
                    row.BillId == bill.Id &&
                    row.Status == CreditApplicationStatus.Applied)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            var paid = await _context.Payments
                .Where(row => row.BillId == bill.Id && row.Status == "Recorded")
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            if (application.Amount > credit.GrandTotal - creditUsed)
            {
                throw new InvalidOperationException(
                    "Amount exceeds the remaining vendor credit. Refresh and try again.");
            }

            if (application.Amount > bill.GrandTotal - paid - billCredits)
            {
                throw new InvalidOperationException(
                    "Amount exceeds the bill outstanding balance. Refresh and try again.");
            }

            application.VendorCreditNumber = credit.VendorCreditNumber!;
            application.BillNumber = bill.BillNumber ?? "";
            application.Currency = credit.Currency;

            _context.Applications.Add(application);

            credit.ModifiedDate = DateTime.UtcNow;
            _context.Entry(credit)
                .Property(row => row.ModifiedDate)
                .IsModified = true;

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return application;
        }

        public async Task<VendorCreditApplication?> VoidAsync(
            int creditId,
            int applicationId,
            VoidCreditApplicationDto dto,
            CancellationToken cancellationToken)
        {
            var snapshot = await _context.Applications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == applicationId && row.VendorCreditId == creditId,
                    cancellationToken);

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

            var credit = await LockCreditAsync(creditId, cancellationToken)
                ?? throw new InvalidOperationException("Vendor credit not found.");

            var application = await _context.Applications
                .FromSqlInterpolated(
                    $"SELECT * FROM [purchases].[VendorCreditApplications] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {applicationId}")
                .SingleAsync(cancellationToken);

            if (application.Status == CreditApplicationStatus.Voided)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return application;
            }

            if (!application.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            application.Status = CreditApplicationStatus.Voided;
            application.VoidedDate = DateTime.UtcNow;
            application.VoidReason = dto.Reason.Trim();

            credit.ModifiedDate = DateTime.UtcNow;
            _context.Entry(credit)
                .Property(row => row.ModifiedDate)
                .IsModified = true;

            _context.Entry(application)
                .Property(row => row.RowVersion)
                .OriginalValue = dto.RowVersion;

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return application;
        }
    }
}
