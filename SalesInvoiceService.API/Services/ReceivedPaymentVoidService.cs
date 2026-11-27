using System.Data;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;
using SparkFinance.Database;

namespace SalesInvoiceService.API.Services
{
    public class ReceivedPaymentVoidService
    {
        private readonly SalesInvoiceDbContext _context;

        public ReceivedPaymentVoidService(SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public async Task<SalesInvoicePayment> VoidAsync(
            int paymentId,
            string reason,
            CancellationToken cancellationToken)
        {
            reason = reason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            {
                throw new ArgumentException("Enter a void reason of up to 1,000 characters.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var resource = "Finance.AutomaticPayment.Received." + paymentId;
            var mySql = _context.Database.IsFinanceMySql();
            if (mySql)
            {
                await MySqlTransactionLock.AcquireAsync(_context, resource, cancellationToken);
            }
            else
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = {resource},
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 51000, 'Payment processing is busy. Retry shortly.', 1;
                """, cancellationToken);
            }

            var paymentRows = mySql
                ? _context.SalesInvoicePayments.LockForUpdate(_context, nameof(SalesInvoicePayment.Id), paymentId)
                : _context.SalesInvoicePayments.FromSqlInterpolated($"""
                    SELECT * FROM sales.SalesInvoicePayments WITH (UPDLOCK,HOLDLOCK)
                    WHERE Id = {paymentId}
                    """);
            var payment = await paymentRows.IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Payment not found.");

            if (payment.Status == "Voided")
            {
                await transaction.CommitAsync(cancellationToken);
                return payment;
            }

            var invoiceRows = mySql
                ? _context.SalesInvoices.LockForUpdate(_context, nameof(SalesInvoice.Id), payment.SalesInvoiceId)
                : _context.SalesInvoices.FromSqlInterpolated($"""
                    SELECT * FROM sales.SalesInvoices WITH (UPDLOCK,HOLDLOCK)
                    WHERE Id = {payment.SalesInvoiceId}
                    """);
            var invoice = await invoiceRows.SingleAsync(cancellationToken);

            var hasJournal = mySql
                ? (await _context.Database.SqlQueryRaw<int>(
                    "SELECT Id AS Value FROM accounting__JournalEntries WHERE Source = 'PaymentReceived' AND SourceDocumentId = {0} FOR UPDATE",
                    paymentId.ToString()).ToListAsync(cancellationToken)).Count
                : await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value] FROM accounting.JournalEntries WITH (UPDLOCK,HOLDLOCK)
                WHERE Source = 'PaymentReceived' AND SourceDocumentId = {paymentId.ToString()}
                """).SingleAsync(cancellationToken);

            var links = mySql
                ? await _context.Database.SqlQueryRaw<ReceivedPaymentVoidLink>(
                    "SELECT BankTransactionId, ReversalTransactionId, AccountingJournalEntryId FROM banking__PaymentBankingLinks WHERE Source = 'Received' AND SourcePaymentId = {0} FOR UPDATE",
                    paymentId).ToListAsync(cancellationToken)
                : await _context.Database.SqlQuery<ReceivedPaymentVoidLink>($"""
                SELECT BankTransactionId, ReversalTransactionId, AccountingJournalEntryId
                FROM banking.PaymentBankingLinks WITH (UPDLOCK,HOLDLOCK)
                WHERE Source = 'Received' AND SourcePaymentId = {paymentId}
                """).ToListAsync(cancellationToken);

            if (hasJournal != 0 || links.Any(row =>
                row.AccountingJournalEntryId.HasValue || row.ReversalTransactionId.HasValue))
            {
                throw new InvalidOperationException(
                    "This payment has an accounting posting or reversal. Use a reviewed reversal instead.");
            }

            var bankIds = links.Where(row => row.BankTransactionId.HasValue)
                .Select(row => row.BankTransactionId!.Value).ToList();
            if (payment.BankTransactionId.HasValue)
            {
                bankIds.Add(payment.BankTransactionId.Value);
            }

            foreach (var bankId in bankIds.Distinct())
            {
                var status = mySql
                    ? await _context.Database.SqlQueryRaw<string>(
                        "SELECT Status AS Value FROM banking__BankTransactions WHERE Id = {0} FOR UPDATE",
                        bankId).SingleOrDefaultAsync(cancellationToken)
                    : await _context.Database.SqlQuery<string>($"""
                    SELECT Status AS [Value] FROM banking.BankTransactions WITH (UPDLOCK,HOLDLOCK)
                    WHERE Id = {bankId}
                    """).SingleOrDefaultAsync(cancellationToken);

                if (status != "Draft" && status != "Cancelled")
                {
                    throw new InvalidOperationException(
                        "Only an unposted bank draft can be cancelled with this payment.");
                }
            }

            var paid = await _context.SalesInvoicePayments
                .Where(row => row.SalesInvoiceId == invoice.Id && row.Id != paymentId)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;
            var credits = await _context.SalesCreditNotes
                .Where(row => row.SalesInvoiceId == invoice.Id && row.Status == SalesCreditNoteStatus.Issued)
                .SumAsync(row => (decimal?)row.GrandTotal, cancellationToken) ?? 0m;
            var applied = await _context.CustomerCreditApplications
                .Where(row => row.TargetInvoiceId == invoice.Id)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;
            var transferred = await _context.CustomerCreditApplications
                .Where(row => row.SourceInvoiceId == invoice.Id)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            if (transferred > Math.Max(0m, paid + credits + applied - invoice.GrandTotal))
            {
                throw new InvalidOperationException(
                    "Customer credit from this payment has been used on another invoice. Review the credit application first.");
            }

            var now = DateTime.UtcNow;
            foreach (var bankId in bankIds.Distinct())
            {
                var bankSql = mySql
                    ? """
                    UPDATE banking__BankTransactions
                    SET Status = 'Cancelled', CancelledDate = {0}, ModifiedDate = {0},
                        Notes = CONCAT(COALESCE(Notes, ''), CHAR(13), CHAR(10), 'Source payment voided: ', {1})
                    WHERE Id = {2} AND Status = 'Draft'
                    """
                    : """
                    UPDATE banking.BankTransactions
                    SET Status = 'Cancelled', CancelledDate = {0}, ModifiedDate = {0},
                        Notes = CONCAT(COALESCE(Notes, ''), CHAR(13), CHAR(10), 'Source payment voided: ', {1})
                    WHERE Id = {2} AND Status = 'Draft'
                    """;
                await _context.Database.ExecuteSqlRawAsync(bankSql, [now, reason, bankId], cancellationToken);
            }

            var linkSql = mySql
                ? """
                UPDATE banking__PaymentBankingLinks
                SET State = 'Cancelled', AccountingState = 'Cancelled',
                    Message = {0}, AccountingMessage = '',
                    UpdatedAtUtc = {1}, NextAttemptAtUtc = {1}
                WHERE Source = 'Received' AND SourcePaymentId = {2}
                """
                : """
                UPDATE banking.PaymentBankingLinks
                SET State = 'Cancelled', AccountingState = 'Cancelled',
                    Message = {0}, AccountingMessage = '',
                    UpdatedAtUtc = {1}, NextAttemptAtUtc = {1}
                WHERE Source = 'Received' AND SourcePaymentId = {2}
                """;
            await _context.Database.ExecuteSqlRawAsync(
                linkSql, ["Source payment voided: " + reason, now, paymentId], cancellationToken);

            payment.Status = "Voided";
            payment.VoidedDate = now;
            payment.VoidReason = reason;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return payment;
        }
    }

    public class ReceivedPaymentVoidLink
    {
        public int? BankTransactionId { get; set; }
        public int? ReversalTransactionId { get; set; }
        public int? AccountingJournalEntryId { get; set; }
    }
}
