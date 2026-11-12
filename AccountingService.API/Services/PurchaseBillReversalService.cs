using System.ComponentModel.DataAnnotations;
using System.Data;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class PurchaseBillReversalService
    {
        private readonly AccountingDbContext _context;
        private readonly IJournalEntryService _journals;

        public PurchaseBillReversalService(AccountingDbContext context, IJournalEntryService journals)
        {
            _context = context;
            _journals = journals;
        }

        public async Task<object> ReverseAsync(int billId, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var status = await _context.Database.SqlQuery<string>($"""
                SELECT Status AS Value FROM purchases.Bills WITH (UPDLOCK,HOLDLOCK) WHERE Id = {billId}
                """).SingleOrDefaultAsync(cancellationToken);
            if (status != "Cancelled")
            {
                throw new ValidationException("Only a cancelled purchase bill can reverse its linked posting.");
            }
            var sourceId = billId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var original = await _context.JournalEntries
                .Where(row => row.Source == JournalEntrySource.PurchaseBill && row.SourceDocumentId == sourceId)
                .SingleOrDefaultAsync(cancellationToken);
            if (original == null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new
                {
                    journalEntryId = 0,
                    status = "NoPosting",
                    alreadyExisted = true
                };
            }
            var existing = await _context.JournalEntries.AsNoTracking()
                .SingleOrDefaultAsync(row => row.ReversalOfJournalEntryId == original.Id, cancellationToken);
            if (existing != null)
            {
                if (existing.Status != JournalEntryStatus.Posted || original.Status != JournalEntryStatus.Reversed)
                {
                    throw new ValidationException("The linked bill reversal requires accounting review.");
                }
                await transaction.CommitAsync(cancellationToken);
                return new
                {
                    journalEntryId = existing.Id,
                    status = "Reversed",
                    alreadyExisted = true
                };
            }
            var reversal = await _journals.ReverseAsync(original.Id, original.CompanyId,
                new JournalEntryActionDto
                {
                    RowVersion = Convert.ToBase64String(original.RowVersion),
                    Reason = "Automatic reversal for cancelled purchase bill " + original.SourceDocumentNumber
                }, cancellationToken)
                ?? throw new ValidationException("The linked purchase bill journal could not be reversed.");
            await transaction.CommitAsync(cancellationToken);
            return new
            {
                journalEntryId = reversal.Id,
                status = "Reversed",
                alreadyExisted = false
            };
        }
    }
}
