using System.Data;
using BankingService.API.Data;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankingService.API.Services
{
    public class PaymentBankingService
    {
        private readonly BankingDbContext _context;
        private readonly PaymentBankingOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymentBankingService> _logger;

        public PaymentBankingService(
            BankingDbContext context,
            IOptions<PaymentBankingOptions> options,
            TimeProvider time,
            ILogger<PaymentBankingService> logger)
        {
            _context = context;
            _options = options.Value;
            _time = time;
            _logger = logger;
        }

        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                return;
            }

            var settings = await _context.PaymentBankingSettings
                .AsNoTracking()
                .SingleAsync(row => row.Id == 1, cancellationToken);
            var since = settings.SourceAfterUtc;

            var candidates = await _context.Database.SqlQuery<PaymentBankingCandidate>($"""
                SELECT TOP (100) 'Made' AS [Source], p.[Id]
                FROM [purchases].[PaymentsMade] p
                WHERE p.[CreatedDate] >= {since} AND p.[BankTransactionId] IS NULL
                AND NOT EXISTS (SELECT 1 FROM [banking].[PaymentBankingLinks] l WHERE l.[Source] = 'Made' AND l.[SourcePaymentId] = p.[Id])
                UNION ALL
                SELECT TOP (100) 'Received' AS [Source], p.[Id]
                FROM [sales].[SalesInvoicePayments] p
                WHERE p.[CreatedDate] >= {since} AND p.[BankTransactionId] IS NULL
                AND NOT EXISTS (SELECT 1 FROM [banking].[PaymentBankingLinks] l WHERE l.[Source] = 'Received' AND l.[SourcePaymentId] = p.[Id])
                """).ToListAsync(cancellationToken);

            var retries = await _context.PaymentBankingLinks.AsNoTracking()
                .Where(row => row.State == "NeedsReview" && row.NextAttemptAtUtc <= _time.GetUtcNow().UtcDateTime)
                .OrderBy(row => row.NextAttemptAtUtc)
                .Take(25)
                .Select(row => new PaymentBankingCandidate { Source = row.Source, Id = row.SourcePaymentId })
                .ToListAsync(cancellationToken);

            // Existing links are also checked for vendor-payment voids and manual draft posting.
            var tracked = await _context.PaymentBankingLinks.AsNoTracking()
                .Where(row => row.BankTransactionId != null && row.State != "Reversed" && row.State != "Cancelled")
                .Select(row => new PaymentBankingCandidate { Source = row.Source, Id = row.SourcePaymentId })
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates.Concat(retries).Concat(tracked)
                .DistinctBy(row => (row.Source, row.Id)))
            {
                try
                {
                    await ProcessAsync(candidate.Source, candidate.Id, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unable to process {Source} payment {PaymentId}. It will retry; the source payment is unchanged.", candidate.Source, candidate.Id);
                }
                finally
                {
                    _context.ChangeTracker.Clear();
                }
            }
        }

        private async Task<PaymentBankingSource?> ReadSourceAsync(
            string source,
            int id,
            CancellationToken cancellationToken)
        {
            if (source == "Made")
            {
                return (await _context.Database.SqlQuery<PaymentBankingSource>($"""
                    SELECT 'Made' AS [Source], [Id], [RequestId], [PaymentNumber], [VendorName] AS [PartyName],
                    [Branch], [Currency], [Amount], [PaymentMode], [Reference], [PaymentDate], [CreatedDate], [VoidedDate], [Status]
                    FROM [purchases].[PaymentsMade] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}
                    """).ToListAsync(cancellationToken)).SingleOrDefault();
            }

            return (await _context.Database.SqlQuery<PaymentBankingSource>($"""
                SELECT 'Received' AS [Source], p.[Id], p.[RequestId], CONCAT('PAY-',FORMAT(p.[Id],'000000')) AS [PaymentNumber],
                i.[ClientName] AS [PartyName], i.[Branch], p.[Currency], p.[Amount], p.[PaymentMode], p.[Reference],
                p.[PaymentDate], p.[CreatedDate], CAST(NULL AS datetime2) AS [VoidedDate], 'Recorded' AS [Status]
                FROM [sales].[SalesInvoicePayments] p WITH (UPDLOCK,HOLDLOCK)
                JOIN [sales].[SalesInvoices] i ON i.[Id] = p.[SalesInvoiceId]
                WHERE p.[Id] = {id}
                """).ToListAsync(cancellationToken)).SingleOrDefault();
        }

        public async Task ProcessAsync(string source, int id, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await BankingPaymentLock.AcquireAsync(_context, source + "." + id, cancellationToken);

            var payment = await ReadSourceAsync(source, id, cancellationToken);
            if (payment == null)
            {
                return;
            }

            var link = await _context.PaymentBankingLinks.SingleOrDefaultAsync(
                row => row.Source == source && row.SourcePaymentId == id, cancellationToken);
            if (link == null)
            {
                link = new PaymentBankingLink
                {
                    Source = source,
                    SourcePaymentId = id,
                    SourceRequestId = payment.RequestId,
                    PaymentNumber = payment.PaymentNumber,
                    Branch = payment.Branch,
                    Currency = payment.Currency,
                    Amount = payment.Amount
                };
                _context.PaymentBankingLinks.Add(link);
            }

            var now = _time.GetUtcNow().UtcDateTime;
            link.UpdatedAtUtc = now;
            link.NextAttemptAtUtc = now.AddMinutes(1);

            if (link.BankTransactionId.HasValue)
            {
                await UpdateExistingAsync(payment, link, cancellationToken);
            }
            else if (payment.Status == "Voided")
            {
                link.State = "Cancelled";
                link.Message = "Payment voided before a bank entry was created.";
            }
            else if (Contains(_options.CashModes, payment.PaymentMode))
            {
                link.State = "NoBank";
                link.Message = "Cash payment: no bank transaction.";
            }
            else
            {
                await CreateEntryAsync(payment, link, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private async Task CreateEntryAsync(
            PaymentBankingSource payment,
            PaymentBankingLink link,
            CancellationToken cancellationToken)
        {
            link.State = "NeedsReview";
            var routes = _options.Routes.Where(route => Contains(route.Branches, payment.Branch)).ToList();
            if (routes.Count != 1)
            {
                link.Message = "Configure one bank route for managing branch " + payment.Branch + ".";
                return;
            }
            var route = routes.Single();
            if (!string.Equals(route.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            {
                link.Message = "Payment currency " + payment.Currency + " differs from branch bank currency " + route.Currency + ". Actual converted bank amount is required; no rate was assumed.";
                return;
            }
            if (!Contains(_options.DraftModes, payment.PaymentMode) && !Contains(_options.PostedModes, payment.PaymentMode))
            {
                link.Message = "Configure banking behavior for payment mode " + payment.PaymentMode + ".";
                return;
            }

            var accounts = await _context.BankAccounts.Where(row => row.IsActive &&
                row.Name == route.BankAccountName && row.Currency == route.Currency)
                .Take(2).ToListAsync(cancellationToken);
            if (accounts.Count != 1)
            {
                link.Message = "Configure one active " + route.Currency + " bank account named " + route.BankAccountName + ".";
                return;
            }
            var account = accounts.Single();
            await BankingPaymentLock.AcquireAsync(_context, "Account." + account.Id, cancellationToken);
            if (account.OpeningBalanceDate == null || payment.PaymentDate < account.OpeningBalanceDate ||
                account.CurrencyDecimalPlaces < 0 || account.CurrencyDecimalPlaces > 4 ||
                payment.Amount <= 0 || decimal.Round(payment.Amount, account.CurrencyDecimalPlaces) != payment.Amount)
            {
                link.Message = "Payment date or amount does not match the bank account opening settings or currency precision.";
                return;
            }

            var closed = await IsClosedAsync(account.Id, payment.PaymentDate, cancellationToken);
            var draft = Contains(_options.DraftModes, payment.PaymentMode) || closed;
            var message = closed ? "Payment date is in a completed bank reconciliation. Review the draft; the original payment date is preserved."
                : draft ? "Awaiting cheque clearance. Confirm the actual bank movement before posting this draft." : "";
            var entry = NewEntry(payment, account, payment.PaymentDate, draft ? "Draft" : "Posted", message);
            _context.BankTransactions.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
            link.BankTransactionId = entry.Id;
            link.State = entry.Status;
            link.Message = message;
        }

        private async Task UpdateExistingAsync(
            PaymentBankingSource payment,
            PaymentBankingLink link,
            CancellationToken cancellationToken)
        {
            var entry = await _context.BankTransactions.Include(row => row.BankAccount)
                .SingleAsync(row => row.Id == link.BankTransactionId, cancellationToken);
            await BankingPaymentLock.AcquireAsync(_context, "Account." + entry.BankAccountId, cancellationToken);
            if (payment.Status != "Voided")
            {
                link.State = entry.Status;
                if (entry.Status != "Draft") link.Message = "";
                return;
            }
            if (entry.Status == "Draft")
            {
                entry.Status = "Cancelled";
                entry.CancelledDate = _time.GetUtcNow().UtcDateTime;
                entry.ModifiedDate = entry.CancelledDate;
                link.State = "Cancelled";
                link.Message = "Source payment voided; unposted draft cancelled.";
                return;
            }
            if (entry.Status == "Cancelled")
            {
                link.State = "Cancelled";
                return;
            }
            if (link.ReversalTransactionId.HasValue)
            {
                link.State = "Reversed";
                return;
            }

            var route = _options.Routes.Single(row => Contains(row.Branches, payment.Branch));
            var utc = DateTime.SpecifyKind(payment.VoidedDate ?? _time.GetUtcNow().UtcDateTime, DateTimeKind.Utc);
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(route.TimeZoneId)));
            if (date < entry.TransactionDate) date = entry.TransactionDate;
            var closed = await IsClosedAsync(entry.BankAccountId, date, cancellationToken);
            var reversal = NewEntry(payment, entry.BankAccount, date, closed ? "Draft" : "Posted", "Source vendor payment voided; Finance reversal only.");
            reversal.Direction = entry.Direction == BankTransactionDirection.MoneyOut
                ? BankTransactionDirection.MoneyIn : BankTransactionDirection.MoneyOut;
            reversal.Amount = entry.Amount;
            reversal.Currency = entry.Currency;
            reversal.CurrencyDecimalPlaces = entry.CurrencyDecimalPlaces;
            reversal.ReversalOfTransactionId = entry.Id;
            reversal.Description = "Reversal of payment " + payment.PaymentNumber + " · " + payment.PartyName;
            _context.BankTransactions.Add(reversal);
            await _context.SaveChangesAsync(cancellationToken);
            link.ReversalTransactionId = reversal.Id;
            link.State = "Reversed";
            link.Message = closed ? "Reversal created as Draft because its date is in a completed reconciliation." : "Payment void reversal posted.";
        }

        private BankTransaction NewEntry(PaymentBankingSource payment, BankAccount account, DateOnly date, string status, string notes)
        {
            var description = (payment.Source == "Made" ? "Payment to " : "Payment received from ") + payment.PartyName + " · " + payment.PaymentNumber;
            return new BankTransaction
            {
                RequestId = Guid.NewGuid(),
                RequestFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payment.Source + "." + payment.Id))),
                BankAccountId = account.Id,
                CompanyId = account.CompanyId,
                TransactionDate = date,
                Direction = payment.Source == "Made" ? BankTransactionDirection.MoneyOut : BankTransactionDirection.MoneyIn,
                Amount = payment.Amount,
                Currency = account.Currency,
                CurrencyDecimalPlaces = account.CurrencyDecimalPlaces,
                Status = status,
                Reference = payment.Reference,
                Description = description,
                Notes = notes,
                CreatedDate = _time.GetUtcNow().UtcDateTime,
                PostedDate = status == "Posted" ? _time.GetUtcNow().UtcDateTime : null
            };
        }

        private Task<bool> IsClosedAsync(int accountId, DateOnly date, CancellationToken cancellationToken)
        {
            return _context.BankReconciliations.AnyAsync(row => row.BankAccountId == accountId &&
                row.Status == "Completed" && row.PeriodEnd >= date, cancellationToken);
        }

        private static bool Contains(IEnumerable<string> values, string value)
        {
            return values.Any(item => string.Equals(item.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    public class PaymentBankingCandidate
    {
        public string Source { get; set; } = "";
        public int Id { get; set; }
    }
}
