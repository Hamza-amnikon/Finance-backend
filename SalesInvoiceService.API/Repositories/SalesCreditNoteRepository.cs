using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;
using System.Globalization;

namespace SalesInvoiceService.API.Repositories
{
    public class SalesCreditNoteRepository
    : ISalesCreditNoteRepository
    {
        private readonly SalesInvoiceDbContext _context;
        private readonly ISalesCreditNoteCalculator _calculator;

        public SalesCreditNoteRepository(
            SalesInvoiceDbContext context,
            ISalesCreditNoteCalculator calculator)
        {
            _context = context;
            _calculator = calculator;
        }

        public async Task<SalesInvoice?> GetInvoiceAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(invoice => invoice.Items)
                .SingleOrDefaultAsync(
                    invoice => invoice.Id == salesInvoiceId,
                    cancellationToken);
        }

        public async Task<List<SalesCreditNoteItem>>
            GetIssuedCreditItemsAsync(
                int salesInvoiceId,
                CancellationToken cancellationToken)
        {
            return await _context.SalesCreditNoteItems
                .AsNoTracking()
                .Where(item =>
                    item.SalesCreditNote.SalesInvoiceId ==
                        salesInvoiceId &&
                    item.SalesCreditNote.Status ==
                        SalesCreditNoteStatus.Issued)
                .OrderBy(item => item.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<SalesCreditNote?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await ReadQuery()
                .SingleOrDefaultAsync(
                    credit => credit.Id == id,
                    cancellationToken);
        }

        public async Task<SalesCreditNote?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return await ReadQuery()
                .SingleOrDefaultAsync(
                    credit => credit.RequestId == requestId,
                    cancellationToken);
        }

        public async Task<(
            List<SalesCreditNote> Items,
            int Total)> GetPagedAsync(
                int page,
                int pageSize,
                string? search,
                SalesCreditNoteStatus? status,
                CancellationToken cancellationToken)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                throw new ArgumentException(
                    "Page must be positive and page size must be between 1 and 100.");
            }

            if (status.HasValue &&
                !Enum.IsDefined(
                    typeof(SalesCreditNoteStatus),
                    status.Value))
            {
                throw new ArgumentException(
                    "Select a valid credit note status.");
            }

            var query = _context.SalesCreditNotes
                .AsNoTracking()
                .AsQueryable();

            var term = search?.Trim();

            if (!string.IsNullOrEmpty(term))
            {
                if (term.Length > 200)
                {
                    throw new ArgumentException(
                        "Search cannot exceed 200 characters.");
                }

                query = query.Where(credit =>
                    (credit.CreditNoteNumber != null &&
                        credit.CreditNoteNumber.Contains(term)) ||
                    (credit.SalesInvoice.InvoiceNumber != null &&
                        credit.SalesInvoice.InvoiceNumber.Contains(term)) ||
                    credit.SalesInvoice.ClientName.Contains(term) ||
                    credit.SalesInvoice.CompanyName.Contains(term) ||
                    credit.Reason.Contains(term));
            }

            if (status.HasValue)
            {
                query = query.Where(
                    credit => credit.Status == status.Value);
            }

            var total = await query.CountAsync(
                cancellationToken);

            var offset = ((long)page - 1) * pageSize;

            if (offset >= total)
            {
                return (new List<SalesCreditNote>(), total);
            }

            var items = await query
                .Include(credit => credit.SalesInvoice)
                .Include(credit => credit.Items)
                .OrderByDescending(credit => credit.CreditNoteDate)
                .ThenByDescending(credit => credit.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<SalesCreditNote> CreateDraftAsync(
            SalesCreditNote creditNote,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(creditNote);

            if (creditNote.Id != 0 ||
                creditNote.Status != SalesCreditNoteStatus.Draft)
            {
                throw new ArgumentException(
                    "Only a new draft credit note can be created.");
            }

            if (creditNote.RequestId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A credit note request ID is required.");
            }

            creditNote.Reason =
                creditNote.Reason?.Trim() ?? string.Empty;

            creditNote.Notes =
                creditNote.Notes?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(creditNote.Reason) ||
                creditNote.Reason.Length > 2000)
            {
                throw new ArgumentException(
                    "Enter a credit reason with no more than 2000 characters.");
            }

            if (creditNote.Notes.Length > 2000)
            {
                throw new ArgumentException(
                    "Notes cannot exceed 2000 characters.");
            }

            if (creditNote.CreditNoteDate == default)
            {
                throw new ArgumentException(
                    "Credit note date is required.");
            }

            if (creditNote.Items == null ||
                creditNote.Items.Count == 0 ||
                creditNote.Items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Select at least one invoice service to credit.");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            // Uses the same invoice lock as payment recording.
            var invoice = await _context.SalesInvoices
                .FromSqlInterpolated(
                    $"SELECT * FROM [sales].[SalesInvoices] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {creditNote.SalesInvoiceId}")
                .Include(record => record.Items)
                .SingleOrDefaultAsync(cancellationToken);

            if (invoice == null)
            {
                throw new ArgumentException(
                    "Invoice not found.");
            }

            var existing = await GetByRequestIdAsync(
                creditNote.RequestId,
                cancellationToken);

            if (existing != null)
            {
                EnsureSameDraft(existing, creditNote);

                await transaction.CommitAsync(
                    cancellationToken);

                return existing;
            }

            if (invoice.Status != SalesInvoiceStatus.Issued)
            {
                throw new InvalidOperationException(
                    "Credit notes can only be created against issued invoices.");
            }

            if (creditNote.CreditNoteDate < invoice.InvoiceDate)
            {
                throw new ArgumentException(
                    "Credit note date cannot be before the invoice date.");
            }

            var currency = await _context.CurrencyMasters
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    record =>
                        record.CurrencyCode == invoice.Currency,
                    cancellationToken);

            if (currency == null)
            {
                throw new InvalidOperationException(
                    "The original invoice currency is not configured.");
            }

            var issuedItems = await GetIssuedCreditItemsAsync(
                invoice.Id,
                cancellationToken);

            _calculator.Calculate(
                creditNote,
                invoice,
                issuedItems,
                currency.DecimalPlaces);

            creditNote.CreditNoteNumber = null;
            creditNote.IssuedDate = null;
            creditNote.CreatedDate = DateTime.UtcNow;
            creditNote.SalesInvoice = invoice;

            _context.SalesCreditNotes.Add(creditNote);

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return creditNote;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                // Another request may have saved this RequestId first.
                await transaction.RollbackAsync(
                    cancellationToken);

                await transaction.DisposeAsync();

                foreach (var item in creditNote.Items)
                {
                    _context.Entry(item).State =
                        EntityState.Detached;
                }

                _context.Entry(creditNote).State =
                    EntityState.Detached;

                var duplicate = await GetByRequestIdAsync(
                    creditNote.RequestId,
                    cancellationToken);

                if (duplicate == null)
                {
                    throw;
                }

                EnsureSameDraft(duplicate, creditNote);

                return duplicate;
            }
        }

        public async Task<SalesCreditNote?> IssueAsync(
            int id,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            if (rowVersion == null || rowVersion.Length != 8)
                throw new ArgumentException("A valid row version is required.");

            var invoiceId = await _context.SalesCreditNotes
                .AsNoTracking()
                .Where(credit => credit.Id == id)
                .Select(credit => (int?)credit.SalesInvoiceId)
                .SingleOrDefaultAsync(cancellationToken);

            if (!invoiceId.HasValue)
                return null;

            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);

            // Lock the invoice first, matching payment recording.
            var invoice = await _context.SalesInvoices
                .FromSqlInterpolated($"SELECT * FROM [sales].[SalesInvoices] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {invoiceId.Value}")
                .Include(record => record.Items)
                .SingleOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new InvalidOperationException("The original invoice could not be found.");

            var creditNote = await _context.SalesCreditNotes
                .FromSqlInterpolated($"SELECT * FROM [sales].[SalesCreditNotes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .Include(credit => credit.Items.OrderBy(item => item.LineNumber))
                .SingleOrDefaultAsync(cancellationToken);

            if (creditNote == null)
                return null;

            if (creditNote.SalesInvoiceId != invoice.Id)
                throw new InvalidOperationException("The credit note invoice link has changed.");

            creditNote.SalesInvoice = invoice;

            // A retry returns the credit note that was already issued.
            if (creditNote.Status == SalesCreditNoteStatus.Issued)
            {
                await transaction.CommitAsync(cancellationToken);
                return creditNote;
            }

            if (creditNote.Status != SalesCreditNoteStatus.Draft)
                throw new InvalidOperationException("Only draft credit notes can be issued.");

            if (!creditNote.RowVersion.SequenceEqual(rowVersion))
                throw new DbUpdateConcurrencyException("The credit note changed. Reload it before issuing.");

            if (invoice.Status != SalesInvoiceStatus.Issued)
                throw new InvalidOperationException("The original invoice must be issued.");

            if (creditNote.CreditNoteDate == default || creditNote.CreditNoteDate < invoice.InvoiceDate)
                throw new ArgumentException("Credit note date must be on or after the invoice date.");

            if (string.IsNullOrWhiteSpace(creditNote.Reason))
                throw new ArgumentException("A credit reason is required.");

            if (!string.IsNullOrEmpty(creditNote.CreditNoteNumber))
                throw new InvalidOperationException("A draft credit note must not already have an issued number.");

            var currency = await _context.CurrencyMasters.AsNoTracking()
                .SingleOrDefaultAsync(record => record.CurrencyCode == invoice.Currency, cancellationToken);

            if (currency == null)
                throw new InvalidOperationException("The original invoice currency is not configured.");

            var issuedItems = await GetIssuedCreditItemsAsync(invoice.Id, cancellationToken);
            var savedTotals = (creditNote.Currency, creditNote.SubTotal, creditNote.DiscountTotal,
                creditNote.TaxTotal, creditNote.GrandTotal);
            var savedLines = creditNote.Items.OrderBy(item => item.SalesInvoiceItemId)
                .Select(item => (item.SalesInvoiceItemId, item.LineSubTotal,
                    item.DiscountAmount, item.TaxAmount, item.LineTotal)).ToList();

            // Recheck available credit while holding the invoice lock.
            _calculator.Calculate(creditNote, invoice, issuedItems, currency.DecimalPlaces);

            var currentTotals = (creditNote.Currency, creditNote.SubTotal, creditNote.DiscountTotal,
                creditNote.TaxTotal, creditNote.GrandTotal);
            var currentLines = creditNote.Items.OrderBy(item => item.SalesInvoiceItemId)
                .Select(item => (item.SalesInvoiceItemId, item.LineSubTotal,
                    item.DiscountAmount, item.TaxAmount, item.LineTotal)).ToList();

            if (savedTotals != currentTotals || !savedLines.SequenceEqual(currentLines))
                throw new InvalidOperationException("Available credit amounts changed after this draft was created. Create a new draft with the current amounts.");

            var periodKey = creditNote.CreditNoteDate.ToString("yyyyMM", CultureInfo.InvariantCulture);
            var counter = await _context.CreditNoteNumberCounters
                .FromSqlInterpolated($"SELECT * FROM [sales].[CreditNoteNumberCounters] WITH (UPDLOCK, HOLDLOCK) WHERE [PeriodKey] = {periodKey}")
                .SingleOrDefaultAsync(cancellationToken);

            if (counter == null)
            {
                counter = new CreditNoteNumberCounter { PeriodKey = periodKey, LastNumber = 0 };
                _context.CreditNoteNumberCounters.Add(counter);
            }

            if (counter.LastNumber < 0 || counter.LastNumber == int.MaxValue)
                throw new InvalidOperationException("The credit note number counter requires review.");

            counter.LastNumber++;
            var year = creditNote.CreditNoteDate.Year.ToString("D4", CultureInfo.InvariantCulture);
            var month = creditNote.CreditNoteDate.Month.ToString("D3", CultureInfo.InvariantCulture);
            var sequence = counter.LastNumber.ToString("D3", CultureInfo.InvariantCulture);

            creditNote.CreditNoteNumber = $"CN/{year}/{month}/{sequence}";
            creditNote.Status = SalesCreditNoteStatus.Issued;
            creditNote.IssuedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return creditNote;
        }

        private static void EnsureSameDraft(
            SalesCreditNote existing,
            SalesCreditNote incoming)
        {
            var savedItems = existing.Items
                .OrderBy(item => item.SalesInvoiceItemId)
                .Select(item => (
                    item.SalesInvoiceItemId,
                    item.Quantity,
                    item.CreditAmount));

            var incomingItems = incoming.Items
                .OrderBy(item => item.SalesInvoiceItemId)
                .Select(item => (
                    item.SalesInvoiceItemId,
                    item.Quantity,
                    item.CreditAmount));

            if (existing.SalesInvoiceId != incoming.SalesInvoiceId ||
                existing.CreditNoteDate != incoming.CreditNoteDate ||
                existing.Reason != incoming.Reason ||
                existing.Notes != incoming.Notes ||
                !savedItems.SequenceEqual(incomingItems))
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different credit note details.");
            }
        }

        private IQueryable<SalesCreditNote> ReadQuery()
        {
            return _context.SalesCreditNotes
                .AsNoTracking()
                .Include(credit => credit.SalesInvoice)
                .Include(credit => credit.Items);
        }
    }
}