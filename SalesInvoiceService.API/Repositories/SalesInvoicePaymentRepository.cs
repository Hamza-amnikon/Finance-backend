using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public class SalesInvoicePaymentRepository
        : ISalesInvoicePaymentRepository
    {
        private readonly SalesInvoiceDbContext _context;

        public SalesInvoicePaymentRepository(
            SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public async Task<SalesInvoicePayment?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            return await _context.SalesInvoicePayments
                .AsNoTracking()
                .Include(payment => payment.SalesInvoice)
                .FirstOrDefaultAsync(
                    payment => payment.RequestId == requestId,
                    cancellationToken);
        }

        public async Task<List<SalesInvoicePayment>> GetByInvoiceIdAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken)
        {
            return await _context.SalesInvoicePayments
                .AsNoTracking()
                .Include(payment => payment.SalesInvoice)
                .Where(payment =>
                    payment.SalesInvoiceId == salesInvoiceId)
                .OrderByDescending(payment => payment.PaymentDate)
                .ThenByDescending(payment => payment.CreatedDate)
                .ThenByDescending(payment => payment.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetIssuedCreditAmountAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken)
        {
            return await _context.SalesCreditNotes
                .AsNoTracking()
                .Where(credit =>
                    credit.SalesInvoiceId == salesInvoiceId &&
                    credit.Status == SalesCreditNoteStatus.Issued)
                .SumAsync(
                    credit => (decimal?)credit.GrandTotal,
                    cancellationToken) ?? 0m;
        }

        public async Task<(
            List<SalesInvoicePayment> Items,
            int Total)> GetPagedAsync(
                int page,
                int pageSize,
                string? search,
                DateOnly? fromDate,
                DateOnly? toDate,
                Guid? clientId,
                string? branch,
                string? currency,
                string? paymentMode,
                CancellationToken cancellationToken)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                throw new ArgumentException(
                    "Page must be positive and page size must be between 1 and 100.");
            }

            if (fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value > toDate.Value)
            {
                throw new ArgumentException(
                    "From date cannot be after to date.");
            }

            var query = _context.SalesInvoicePayments
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

                var numberText = term.StartsWith(
                    "PAY-",
                    StringComparison.OrdinalIgnoreCase)
                        ? term[4..]
                        : term;

                var paymentId = int.TryParse(
                    numberText,
                    out var parsedId)
                        ? parsedId
                        : -1;

                query = query.Where(payment =>
                    payment.Id == paymentId ||
                    (payment.SalesInvoice.InvoiceNumber != null &&
                        payment.SalesInvoice.InvoiceNumber.Contains(term)) ||
                    payment.SalesInvoice.ClientName.Contains(term) ||
                    payment.SalesInvoice.CompanyName.Contains(term) ||
                    payment.Reference.Contains(term));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(payment =>
                    payment.PaymentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(payment =>
                    payment.PaymentDate <= toDate.Value);
            }

            if (clientId.HasValue)
            {
                query = query.Where(payment =>
                    payment.SalesInvoice.ClientId == clientId.Value);
            }

            var branchName = branch?.Trim();

            if (!string.IsNullOrEmpty(branchName))
            {
                query = query.Where(payment =>
                    payment.SalesInvoice.Branch == branchName);
            }

            var currencyCode = currency?.Trim();

            if (!string.IsNullOrEmpty(currencyCode))
            {
                query = query.Where(payment =>
                    payment.Currency == currencyCode);
            }

            var mode = paymentMode?.Trim();

            if (!string.IsNullOrEmpty(mode))
            {
                query = query.Where(payment =>
                    payment.PaymentMode == mode);
            }

            var total = await query.CountAsync(
                cancellationToken);

            var offset = ((long)page - 1) * pageSize;

            if (offset >= total)
            {
                return (new List<SalesInvoicePayment>(), total);
            }

            var items = await query
                .Include(payment => payment.SalesInvoice)
                .OrderByDescending(payment => payment.PaymentDate)
                .ThenByDescending(payment => payment.CreatedDate)
                .ThenByDescending(payment => payment.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<SalesInvoicePayment> CreateAsync(
            SalesInvoicePayment payment,
            CancellationToken cancellationToken)
        {
            if (payment.RequestId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A payment request ID is required.");
            }

            if (payment.Amount <= 0 ||
                payment.Amount > 99999999999999.9999m ||
                decimal.Round(payment.Amount, 4) != payment.Amount)
            {
                throw new ArgumentException(
                    "Enter a positive payment amount with no more than four decimal places.");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            // Payments for this invoice must wait for this lock.
            var invoice = await _context.SalesInvoices
                .FromSqlInterpolated(
                    $"SELECT * FROM [sales].[SalesInvoices] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {payment.SalesInvoiceId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
            {
                throw new ArgumentException(
                    "Invoice not found.");
            }

            payment.Currency = invoice.Currency;
            payment.PaymentMode = Clean(payment.PaymentMode);
            payment.Reference = Clean(payment.Reference);
            payment.Notes = Clean(payment.Notes);

            var existing = await GetByRequestIdAsync(
                payment.RequestId,
                cancellationToken);

            if (existing != null)
            {
                EnsureSamePayment(existing, payment);

                await transaction.CommitAsync(cancellationToken);

                return existing;
            }

            if (invoice.Status != SalesInvoiceStatus.Issued)
            {
                throw new ArgumentException(
                    "Payments can only be recorded against issued invoices.");
            }

            var paidAmount = await _context.SalesInvoicePayments
                .Where(entry =>
                    entry.SalesInvoiceId == invoice.Id)
                .SumAsync(
                    entry => (decimal?)entry.Amount,
                    cancellationToken) ?? 0m;

            var creditedAmount = await GetIssuedCreditAmountAsync(
                invoice.Id,
                cancellationToken);

            if (creditedAmount < 0 ||
                creditedAmount > invoice.GrandTotal)
            {
                throw new InvalidOperationException(
                    "Issued credits exceed the invoice total. Review the credit notes.");
            }

            var balanceDue =
                invoice.GrandTotal - creditedAmount - paidAmount;

            if (balanceDue <= 0)
            {
                throw new ArgumentException(
                    "This invoice has no remaining balance.");
            }

            if (payment.Amount > balanceDue)
            {
                throw new ArgumentException(
                    $"Payment exceeds the remaining balance of {balanceDue} {invoice.Currency}.");
            }

            payment.CreatedDate = DateTime.UtcNow;
            payment.SalesInvoice = invoice;

            _context.SalesInvoicePayments.Add(payment);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return payment;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                // Another request may have saved this RequestId first.
                await transaction.RollbackAsync(cancellationToken);
                await transaction.DisposeAsync();

                _context.Entry(payment).State = EntityState.Detached;

                var duplicate = await GetByRequestIdAsync(
                    payment.RequestId,
                    cancellationToken);

                if (duplicate == null)
                {
                    throw;
                }

                EnsureSamePayment(duplicate, payment);

                return duplicate;
            }
        }

        private static void EnsureSamePayment(
            SalesInvoicePayment existing,
            SalesInvoicePayment incoming)
        {
            if (existing.SalesInvoiceId != incoming.SalesInvoiceId ||
                existing.PaymentDate != incoming.PaymentDate ||
                existing.Amount != incoming.Amount ||
                !string.Equals(
                    existing.PaymentMode,
                    incoming.PaymentMode,
                    StringComparison.OrdinalIgnoreCase) ||
                existing.Reference != incoming.Reference ||
                existing.Notes != incoming.Notes)
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different payment details.");
            }
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}