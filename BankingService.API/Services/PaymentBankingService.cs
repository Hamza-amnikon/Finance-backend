using System.Data;
using BankingService.API.Data;
using BankingService.API.DTOs;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankingService.API.Services
{
    public class PaymentBankingService
    {
        private readonly BankingDbContext _context;
        private readonly PaymentBankingOptions _options;
        private readonly IPaymentAccountingApiService _accounting;
        private readonly IBankingExchangeRateService _exchangeRates;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymentBankingService> _logger;

        public PaymentBankingService(
            BankingDbContext context,
            IOptions<PaymentBankingOptions> options,
            IPaymentAccountingApiService accounting,
            IBankingExchangeRateService exchangeRates,
            TimeProvider time,
            ILogger<PaymentBankingService> logger)
        {
            _context = context;
            _options = options.Value;
            _accounting = accounting;
            _exchangeRates = exchangeRates;
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
                AND NOT EXISTS (
                    SELECT 1
                    FROM [banking].[PaymentBankingLinks] l
                    WHERE l.[Source] = 'Made'
                    AND l.[SourcePaymentId] = p.[Id])
                UNION ALL
                SELECT TOP (100) 'Received' AS [Source], p.[Id]
                FROM [sales].[SalesInvoicePayments] p
                WHERE p.[CreatedDate] >= {since} AND p.[BankTransactionId] IS NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM [banking].[PaymentBankingLinks] l
                    WHERE l.[Source] = 'Received'
                    AND l.[SourcePaymentId] = p.[Id])
                UNION ALL
                SELECT TOP (100) 'Expense' AS [Source], e.[Id]
                FROM [expenses].[Expenses] e
                WHERE e.[CreatedDate] >= {since} AND e.[Status] = 'Recorded' AND e.[BankAccountId] IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM [banking].[PaymentBankingLinks] l
                    WHERE l.[Source] = 'Expense'
                    AND l.[SourcePaymentId] = e.[Id])
                """).ToListAsync(cancellationToken);

            var retries = await _context.PaymentBankingLinks.AsNoTracking()
                .Where(row =>
                    (
                        row.State == "NeedsReview" ||
                        row.AccountingState == "NeedsReview" ||
                        (
                            (row.State == "Posted" ||
                             row.State == "NoBank") &&
                            row.AccountingJournalEntryId == null &&
                            (row.AccountingState == "" ||
                             row.AccountingState == "Pending")
                        )
                    ) &&
                    row.NextAttemptAtUtc <= _time.GetUtcNow().UtcDateTime)
                .OrderBy(row => row.NextAttemptAtUtc)
                .Take(25)
                .Select(row => new PaymentBankingCandidate { Source = row.Source, Id = row.SourcePaymentId })
                .ToListAsync(cancellationToken);

            // Existing links are also checked for vendor-payment voids and manual draft posting.
            var tracked = await _context.PaymentBankingLinks.AsNoTracking()
                .Where(row =>
                    (row.BankTransactionId != null &&
                     row.State != "Reversed" &&
                     row.State != "Cancelled") ||
                    ((row.Source == "Received" ||
                      row.Source == "Made") &&
                     row.State == "NoBank" &&
                     row.AccountingJournalEntryId == null))
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
                    _logger.LogError(
                        exception,
                        "Unable to process {Source} payment {PaymentId}. " +
                        "It will retry; the source payment is unchanged.",
                        candidate.Source,
                        candidate.Id);
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
                    SELECT 'Made' AS [Source], p.[Id], p.[RequestId], p.[PaymentNumber],
                    p.[VendorName] AS [PartyName], p.[VendorId] AS [PartyId],
                    p.[Branch], p.[Currency], p.[Amount],
                    p.[BillId],
                    CAST(NULL AS int) AS [SalesInvoiceId],
                    p.[BankAccountId],
                    p.[BankAmount], CAST(0 AS decimal(18,4)) AS [BankChargeAmount],
                    COALESCE(p.[BankCurrency], '') AS [BankCurrency],
                    COALESCE(b.[AccountingCompanyId], 0) AS [CompanyId],
                    p.[PaymentMode], p.[Reference], p.[PaymentDate], p.[CreatedDate],
                    p.[VoidedDate], p.[Status]
                    FROM [purchases].[PaymentsMade] p WITH (UPDLOCK,HOLDLOCK)
                    LEFT JOIN [sales].[BranchMasters] b
                        ON b.[Name] = p.[Branch]
                        AND b.[IsActive] = 1
                    WHERE p.[Id] = {id}
                    """).ToListAsync(cancellationToken)).SingleOrDefault();
            }

            if (source == "Expense")
            {
                return (await _context.Database.SqlQuery<PaymentBankingSource>($"""
                    SELECT 'Expense' AS [Source], e.[Id], e.[RequestId],
                    CONCAT('EXP-', FORMAT(e.[Id], '000000')) AS [PaymentNumber],
                    e.[Supplier] AS [PartyName],
                    CAST(NULL AS uniqueidentifier) AS [PartyId],
                    '' AS [Branch], e.[Currency], e.[Amount], '' AS [PaymentMode],
                    CAST(NULL AS int) AS [BillId],
                    CAST(NULL AS int) AS [SalesInvoiceId], e.[BankAccountId],
                    CAST(NULL AS decimal(18,4)) AS [BankAmount],
                    CAST(0 AS decimal(18,4)) AS [BankChargeAmount],
                    CAST('' AS nvarchar(3)) AS [BankCurrency],
                    e.[CompanyId], e.[Reference], e.[Date] AS [PaymentDate], e.[CreatedDate],
                    CASE WHEN e.[Status] = 'Voided'
                        THEN e.[ModifiedDate]
                        ELSE CAST(NULL AS datetime2)
                    END AS [VoidedDate], e.[Status]
                    FROM [expenses].[Expenses] e WITH (UPDLOCK,HOLDLOCK)
                    WHERE e.[Id] = {id}
                    """).ToListAsync(cancellationToken)).SingleOrDefault();
            }

            return (await _context.Database.SqlQuery<PaymentBankingSource>($"""
                SELECT 'Received' AS [Source], p.[Id], p.[RequestId],
                CONCAT('PAY-',FORMAT(p.[Id],'000000')) AS [PaymentNumber],
                i.[ClientName] AS [PartyName], i.[ClientId] AS [PartyId],
                i.[Branch], p.[Currency], p.[Amount],
                CAST(NULL AS int) AS [BillId],
                p.[SalesInvoiceId], p.[BankAccountId], p.[BankAmount], p.[BankChargeAmount],
                COALESCE(p.[BankCurrency], '') AS [BankCurrency],
                COALESCE(b.[AccountingCompanyId], 0) AS [CompanyId], p.[PaymentMode], p.[Reference],
                p.[PaymentDate], p.[CreatedDate],
                CAST(NULL AS datetime2) AS [VoidedDate], 'Recorded' AS [Status]
                FROM [sales].[SalesInvoicePayments] p WITH (UPDLOCK,HOLDLOCK)
                JOIN [sales].[SalesInvoices] i ON i.[Id] = p.[SalesInvoiceId]
                LEFT JOIN [sales].[BranchMasters] b ON b.[Name] = i.[Branch] AND b.[IsActive] = 1
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

            await PostAccountingAsync(
                payment,
                link,
                cancellationToken);
        }

        public async Task ResolveReceivedAsync(
            int paymentId,
            ResolveReceivedPaymentDto dto,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            await BankingPaymentLock.AcquireAsync(
                _context,
                "Received." + paymentId,
                cancellationToken);

            var payment =
                await ReadSourceAsync(
                    "Received",
                    paymentId,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    "The received payment was not found.");

            var link =
                await _context.PaymentBankingLinks
                    .SingleOrDefaultAsync(
                        row =>
                            row.Source == "Received" &&
                            row.SourcePaymentId == paymentId,
                        cancellationToken);

            if (link?.BankTransactionId != null)
            {
                throw new InvalidOperationException(
                    "This payment already has a bank transaction and cannot be changed here.");
            }

            var account =
                await _context.BankAccounts
                    .SingleOrDefaultAsync(
                        row =>
                            row.Id == dto.BankAccountId &&
                            row.IsActive,
                        cancellationToken)
                ?? throw new InvalidOperationException(
                    "The selected receiving bank account is missing or inactive.");

            if (!account.AccountingAccountId.HasValue)
            {
                throw new InvalidOperationException(
                    "Link the bank account to an accounting ledger before using it.");
            }

            if (
                payment.CompanyId <= 0 ||
                payment.CompanyId != account.CompanyId)
            {
                throw new InvalidOperationException(
                    "Select a bank account belonging to the payment branch's accounting company.");
            }

            if (
                account.CurrencyDecimalPlaces < 0 ||
                account.CurrencyDecimalPlaces > 4 ||
                decimal.Round(
                    dto.BankAmount,
                    account.CurrencyDecimalPlaces) !=
                    dto.BankAmount ||
                decimal.Round(
                    dto.BankChargeAmount,
                    account.CurrencyDecimalPlaces) !=
                    dto.BankChargeAmount)
            {
                throw new InvalidOperationException(
                    "The bank amount or charge has more decimal places than the selected bank account supports.");
            }

            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [sales].[SalesInvoicePayments]
                SET [SettlementType] = 'Bank',
                    [BankAccountId] = {account.Id},
                    [BankAmount] = {dto.BankAmount},
                    [BankChargeAmount] = {dto.BankChargeAmount},
                    [BankAccountName] = {account.Name},
                    [BankCurrency] = {account.Currency}
                WHERE [Id] = {paymentId}
                """, cancellationToken);

            if (link != null)
            {
                link.State = "NeedsReview";
                link.Message = "";
                link.AccountingState = "";
                link.AccountingMessage = "";
                link.NextAttemptAtUtc =
                    _time.GetUtcNow().UtcDateTime;
                link.UpdatedAtUtc =
                    _time.GetUtcNow().UtcDateTime;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _context.ChangeTracker.Clear();

            await ProcessAsync(
                "Received",
                paymentId,
                cancellationToken);
        }

        private async Task CreateEntryAsync(
            PaymentBankingSource payment,
            PaymentBankingLink link,
            CancellationToken cancellationToken)
        {
            link.State = "NeedsReview";

            if (payment.Source == "Expense")
            {
                var expenseAccount = payment.BankAccountId.HasValue
                    ? await _context.BankAccounts.SingleOrDefaultAsync(
                        row => row.Id == payment.BankAccountId.Value,
                        cancellationToken)
                    : null;

                if (expenseAccount == null || expenseAccount.CompanyId != payment.CompanyId || !expenseAccount.IsActive)
                {
                    link.Message = "The expense bank account is missing, inactive, or belongs to another company.";
                    return;
                }

                await BankingPaymentLock.AcquireAsync(_context, "Account." + expenseAccount.Id, cancellationToken);
                if (
                    expenseAccount.OpeningBalanceDate == null ||
                    payment.PaymentDate <
                        expenseAccount.OpeningBalanceDate ||
                    expenseAccount.Currency != payment.Currency || expenseAccount.CurrencyDecimalPlaces < 0 ||
                    expenseAccount.CurrencyDecimalPlaces > 4 || payment.Amount <= 0 ||
                    decimal.Round(payment.Amount, expenseAccount.CurrencyDecimalPlaces) != payment.Amount)
                {
                    link.Message =
                        "Expense date, amount, or currency does not " +
                        "match the selected bank account settings.";
                    return;
                }

                var closedExpensePeriod =
                    await IsClosedAsync(
                        expenseAccount.Id,
                        payment.PaymentDate,
                        cancellationToken);
                var expenseStatus = closedExpensePeriod ? "Draft" : "Posted";
                var expenseMessage = closedExpensePeriod
                    ? "Expense date is in a completed bank reconciliation. Review the transaction date before posting."
                    : "";
                var expenseEntry = NewEntry(
                    payment,
                    expenseAccount,
                    payment.PaymentDate,
                    expenseStatus,
                    expenseMessage);
                _context.BankTransactions.Add(expenseEntry);
                await _context.SaveChangesAsync(cancellationToken);
                link.BankTransactionId = expenseEntry.Id;
                link.State = expenseEntry.Status;
                link.Message = expenseMessage;
                return;
            }

            if (
                !Contains(
                    _options.DraftModes,
                    payment.PaymentMode) &&
                !Contains(
                    _options.PostedModes,
                    payment.PaymentMode))
            {
                link.Message = "Configure banking behavior for payment mode " + payment.PaymentMode + ".";
                return;
            }

            BankAccount? account;

            if (payment.BankAccountId.HasValue)
            {
                account = await _context.BankAccounts
                    .SingleOrDefaultAsync(
                        row =>
                            row.Id == payment.BankAccountId.Value &&
                            row.IsActive,
                        cancellationToken);

                if (account == null)
                {
                    link.Message =
                        "The selected receiving bank account is missing or inactive.";
                    return;
                }
            }
            else
            {
                var routes = _options.Routes
                    .Where(route =>
                        Contains(
                            route.Branches,
                            payment.Branch))
                    .ToList();

                if (routes.Count != 1)
                {
                    link.Message =
                        "Configure one bank route for managing branch " +
                        payment.Branch + ".";
                    return;
                }

                var route = routes.Single();

                if (!string.Equals(
                    route.Currency,
                    payment.Currency,
                    StringComparison.OrdinalIgnoreCase))
                {
                    link.Message =
                        "Payment currency " + payment.Currency +
                        " differs from branch bank currency " +
                        route.Currency +
                        ". Select the receiving bank and enter the actual converted bank amount.";
                    return;
                }

                var accounts = await _context.BankAccounts
                    .Where(row =>
                        row.IsActive &&
                        row.Name == route.BankAccountName &&
                        row.Currency == route.Currency)
                    .Take(2)
                    .ToListAsync(cancellationToken);

                if (accounts.Count != 1)
                {
                    link.Message =
                        "Configure one active " + route.Currency +
                        " bank account named " +
                        route.BankAccountName + ".";
                    return;
                }

                account = accounts.Single();
            }

            if (
                payment.CompanyId <= 0 ||
                account.CompanyId != payment.CompanyId)
            {
                link.Message =
                    "Link the payment branch to the same accounting company as its bank account.";
                return;
            }

            if (
                !string.IsNullOrWhiteSpace(
                    payment.BankCurrency) &&
                !string.Equals(
                    payment.BankCurrency,
                    account.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                link.Message =
                    "The settlement currency does not match the selected bank account.";
                return;
            }

            var bankAmount =
                payment.BankAmount ??
                (
                    string.Equals(
                        payment.Currency,
                        account.Currency,
                        StringComparison.OrdinalIgnoreCase)
                        ? payment.Amount
                        : 0m
                );

            if (bankAmount <= 0)
            {
                link.Message =
                    "Enter the actual " + account.Currency +
                    " amount credited by the bank.";
                return;
            }

            await BankingPaymentLock.AcquireAsync(_context, "Account." + account.Id, cancellationToken);
            if (account.OpeningBalanceDate == null || payment.PaymentDate < account.OpeningBalanceDate ||
                account.CurrencyDecimalPlaces < 0 || account.CurrencyDecimalPlaces > 4 ||
                decimal.Round(bankAmount, account.CurrencyDecimalPlaces) != bankAmount)
            {
                link.Message =
                    "Payment date or amount does not match the bank " +
                    "account opening settings or currency precision.";
                return;
            }

            var closed = await IsClosedAsync(account.Id, payment.PaymentDate, cancellationToken);
            var draft = Contains(_options.DraftModes, payment.PaymentMode) || closed;
            var message = closed
                ? "Payment date is in a completed bank reconciliation. " +
                    "Review the draft; the original payment date is preserved."
                : draft
                    ? "Awaiting cheque clearance. Confirm the actual " +
                        "bank movement before posting this draft."
                    : "";
            var entry = NewEntry(payment, account, payment.PaymentDate, draft ? "Draft" : "Posted", message);
            entry.Amount = bankAmount;
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

            var utc = DateTime.SpecifyKind(payment.VoidedDate ?? _time.GetUtcNow().UtcDateTime, DateTimeKind.Utc);
            var date = payment.Source == "Expense"
                ? DateOnly.FromDateTime(utc)
                : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                    utc,
                    TimeZoneInfo.FindSystemTimeZoneById(
                        _options.Routes.Single(row => Contains(row.Branches, payment.Branch)).TimeZoneId)));
            if (date < entry.TransactionDate) date = entry.TransactionDate;
            var closed = await IsClosedAsync(entry.BankAccountId, date, cancellationToken);
            var reversalNotes = payment.Source == "Expense"
                ? "Source expense voided; Finance reversal only."
                : "Source vendor payment voided; Finance reversal only.";
            var reversal = NewEntry(payment, entry.BankAccount, date, closed ? "Draft" : "Posted", reversalNotes);
            reversal.Direction = entry.Direction == BankTransactionDirection.MoneyOut
                ? BankTransactionDirection.MoneyIn : BankTransactionDirection.MoneyOut;
            reversal.Amount = entry.Amount;
            reversal.Currency = entry.Currency;
            reversal.CurrencyDecimalPlaces = entry.CurrencyDecimalPlaces;
            reversal.ReversalOfTransactionId = entry.Id;
            reversal.Description = payment.Source == "Expense"
                ? "Reversal of expense " + payment.PaymentNumber + " · " + payment.PartyName
                : "Reversal of payment " + payment.PaymentNumber + " · " + payment.PartyName;
            _context.BankTransactions.Add(reversal);
            await _context.SaveChangesAsync(cancellationToken);
            link.ReversalTransactionId = reversal.Id;
            link.State = "Reversed";
            link.Message = closed
                ? "Reversal created as Draft because its date is in a completed reconciliation."
                : payment.Source == "Expense"
                    ? "Expense void reversal posted."
                    : "Payment void reversal posted.";
        }

        private BankTransaction NewEntry(
            PaymentBankingSource payment,
            BankAccount account,
            DateOnly date,
            string status,
            string notes)
        {
            var description = payment.Source switch
            {
                "Made" => "Payment to " + payment.PartyName + " · " + payment.PaymentNumber,
                "Received" => "Payment received from " + payment.PartyName + " · " + payment.PaymentNumber,
                _ => "Expense to " + payment.PartyName + " · " + payment.PaymentNumber
            };
            return new BankTransaction
            {
                RequestId = Guid.NewGuid(),
                RequestFingerprint =
                    Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(
                                payment.Source + "." + payment.Id))),
                BankAccountId = account.Id,
                CompanyId = account.CompanyId,
                TransactionDate = date,
                Direction = payment.Source == "Received"
                    ? BankTransactionDirection.MoneyIn
                    : BankTransactionDirection.MoneyOut,
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

        private async Task PostAccountingAsync(
            PaymentBankingSource payment,
            PaymentBankingLink link,
            CancellationToken cancellationToken)
        {
            if (
                (payment.Source != "Received" &&
                 payment.Source != "Made") ||
                link.AccountingJournalEntryId.HasValue ||
                (link.State != "Posted" &&
                 link.State != "NoBank"))
            {
                return;
            }

            try
            {
                var companyId = payment.CompanyId;
                int? bankAccountingAccountId = null;
                var isCash = link.State == "NoBank";
                BankTransaction? bankTransaction = null;

                if (!isCash)
                {
                    bankTransaction =
                        await _context.BankTransactions
                            .AsNoTracking()
                            .Include(transaction =>
                                transaction.BankAccount)
                            .SingleAsync(
                                transaction =>
                                    transaction.Id ==
                                    link.BankTransactionId,
                                cancellationToken);

                    if (bankTransaction.Status != "Posted")
                    {
                        return;
                    }

                    companyId =
                        bankTransaction.CompanyId;

                    bankAccountingAccountId =
                        bankTransaction.BankAccount
                            .AccountingAccountId;

                    if (!bankAccountingAccountId.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Link the bank account to an accounting " +
                            "ledger before posting payments.");
                    }
                }

                if (companyId <= 0)
                {
                    throw new InvalidOperationException(
                        "Link the payment branch to an accounting company.");
                }

                var company =
                    await _context.Companies
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            row =>
                                row.Id == companyId,
                            cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The payment accounting company is unavailable.");

                var convertedBankPayment =
                    bankTransaction != null &&
                    !string.Equals(
                        payment.Currency,
                        bankTransaction.Currency,
                        StringComparison.OrdinalIgnoreCase);

                var rate = convertedBankPayment
                    ? new BankingExchangeRateDto
                    {
                        Rate = decimal.Round(
                            (
                                bankTransaction!.Amount +
                                payment.BankChargeAmount
                            ) /
                            payment.Amount,
                            8,
                            MidpointRounding.AwayFromZero),
                        RateDate = payment.PaymentDate,
                        Source = "Actual bank settlement"
                    }
                    : await _exchangeRates.GetRateAsync(
                        payment.Currency,
                        company.Currency,
                        payment.PaymentDate,
                        cancellationToken);

                if (payment.Source == "Received")
                {
                    var customerId =
                        payment.PartyId
                        ?? throw new InvalidOperationException(
                            "The payment customer is unavailable.");

                    var result =
                        await _accounting.PostPaymentReceivedAsync(
                            new PaymentReceivedAccountingPostingDto
                            {
                                CompanyId = companyId,
                                PaymentId = payment.Id,
                                SalesInvoiceId =
                                    payment.SalesInvoiceId,
                                PaymentNumber =
                                    payment.PaymentNumber,
                                PaymentDate =
                                    payment.PaymentDate,
                                Branch = payment.Branch,
                                Currency = payment.Currency,
                                ExchangeRate = rate.Rate,
                                SettlementCurrency =
                                    bankTransaction?.Currency ??
                                    payment.Currency,
                                SettlementAmount =
                                    bankTransaction?.Amount ??
                                    payment.Amount,
                                BankChargeAmount =
                                    payment.BankChargeAmount,
                                CustomerId = customerId,
                                CustomerName =
                                    payment.PartyName,
                                BankAccountingAccountId =
                                    bankAccountingAccountId,
                                IsCash = isCash,
                                Amount = payment.Amount,
                                Reference =
                                    payment.Reference
                            },
                            cancellationToken);

                    link.AccountingJournalEntryId =
                        result.JournalEntryId;

                    link.AccountingState =
                        result.Status;
                }
                else
                {
                    var vendorId =
                        payment.PartyId
                        ?? throw new InvalidOperationException(
                            "The payment vendor is unavailable.");

                    var billId =
                        payment.BillId
                        ?? throw new InvalidOperationException(
                            "The payment purchase bill is unavailable.");

                    var result =
                        await _accounting.PostPaymentMadeAsync(
                            new PaymentMadeAccountingPostingDto
                            {
                                CompanyId = companyId,
                                PaymentId = payment.Id,
                                BillId = billId,
                                PaymentNumber =
                                    payment.PaymentNumber,
                                PaymentDate =
                                    payment.PaymentDate,
                                Branch = payment.Branch,
                                Currency = payment.Currency,
                                ExchangeRate = rate.Rate,
                                SettlementCurrency =
                                    bankTransaction?.Currency ??
                                    payment.Currency,
                                SettlementAmount =
                                    bankTransaction?.Amount ??
                                    payment.Amount,
                                VendorId = vendorId,
                                VendorName =
                                    payment.PartyName,
                                BankAccountingAccountId =
                                    bankAccountingAccountId,
                                IsCash = isCash,
                                Amount = payment.Amount,
                                Reference =
                                    payment.Reference
                            },
                            cancellationToken);

                    link.AccountingJournalEntryId =
                        result.JournalEntryId;

                    link.AccountingState =
                        result.Status;
                }

                link.AccountingMessage = "";
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                link.AccountingState =
                    "NeedsReview";

                link.AccountingMessage =
                    exception.Message.Length <= 1000
                        ? exception.Message
                        : exception.Message[..1000];

                link.NextAttemptAtUtc =
                    _time.GetUtcNow()
                        .UtcDateTime
                        .AddMinutes(1);

                _logger.LogWarning(
                    exception,
                    "{Source} payment {PaymentId} accounting will retry.",
                    payment.Source,
                    payment.Id);
            }

            await _context.SaveChangesAsync(
                cancellationToken);
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
