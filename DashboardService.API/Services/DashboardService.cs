using System.ComponentModel.DataAnnotations;
using DashboardService.API.Data;
using DashboardService.API.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DashboardService.API.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly DashboardDbContext _context;
        private readonly IDashboardExchangeRateService _exchangeRates;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            DashboardDbContext context,
            IDashboardExchangeRateService exchangeRates,
            ILogger<DashboardService> logger)
        {
            _context = context;
            _exchangeRates = exchangeRates;
            _logger = logger;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(
            DateOnly from,
            DateOnly to,
            string branch,
            string currency,
            CancellationToken cancellationToken)
        {
            if (from > to)
            {
                throw new ValidationException(
                    "The start date must be on or before the end date.");
            }

            branch = branch.Trim();
            currency = currency.ToUpperInvariant();

            const string sql = @"
                WITH invoiceBalances AS
                (
                    SELECT i.Currency,
                        i.GrandTotal - COALESCE(p.Paid, 0) - COALESCE(c.Credited, 0) AS Balance
                    FROM sales.SalesInvoices i
                    OUTER APPLY
                    (
                        SELECT SUM(Amount) AS Paid
                        FROM sales.SalesInvoicePayments
                        WHERE Status = N'Recorded' AND SalesInvoiceId = i.Id
                    ) p
                    OUTER APPLY
                    (
                        SELECT SUM(GrandTotal) AS Credited
                        FROM sales.SalesCreditNotes
                        WHERE SalesInvoiceId = i.Id AND Status = N'Issued'
                    ) c
                    WHERE i.Status = N'Issued'
                        AND (@branch = N'' OR i.Branch = @branch)
                )
                SELECT Metric, Amount, Currency
                FROM
                (
                    SELECT N'revenue' AS Metric, i.GrandTotal AS Amount, i.Currency
                    FROM sales.SalesInvoices i
                    WHERE i.Status = N'Issued'
                        AND i.InvoiceDate BETWEEN @from AND @to
                        AND (@branch = N'' OR i.Branch = @branch)
                    UNION ALL
                    SELECT N'revenue', -cn.GrandTotal, cn.Currency
                    FROM sales.SalesCreditNotes cn
                    INNER JOIN sales.SalesInvoices i ON i.Id = cn.SalesInvoiceId
                    WHERE cn.Status = N'Issued'
                        AND cn.CreditNoteDate BETWEEN @from AND @to
                        AND (@branch = N'' OR i.Branch = @branch)
                    UNION ALL
                    SELECT N'received', p.Amount, p.Currency
                    FROM sales.SalesInvoicePayments p
                    INNER JOIN sales.SalesInvoices i ON i.Id = p.SalesInvoiceId
                    WHERE p.Status = N'Recorded' AND p.PaymentDate BETWEEN @from AND @to
                        AND i.Status = N'Issued'
                        AND (@branch = N'' OR i.Branch = @branch)
                    UNION ALL
                    SELECT N'expenses', e.Amount, e.Currency
                    FROM expenses.Expenses e
                    INNER JOIN expenses.Companies c ON c.Id = e.CompanyId
                    WHERE e.Status = N'Recorded'
                        AND e.Date BETWEEN @from AND @to
                        AND (@branch = N'' OR c.Name LIKE N'%' + @branch + N'%')
                    UNION ALL
                    SELECT N'expenses', b.GrandTotal, b.Currency
                    FROM purchases.Bills b
                    WHERE b.Status = N'Issued'
                        AND b.BillDate BETWEEN @from AND @to
                        AND (@branch = N'' OR b.Branch = @branch)
                    UNION ALL
                    SELECT N'paid', p.Amount, p.Currency
                    FROM purchases.PaymentsMade p
                    WHERE p.Status = N'Recorded'
                        AND p.PaymentDate BETWEEN @from AND @to
                        AND (@branch = N'' OR p.Branch = @branch)
                    UNION ALL
                    SELECT N'accountsReceivable', Balance, Currency
                    FROM invoiceBalances
                    WHERE Balance > 0
                    UNION ALL
                    SELECT N'customerCredits', -Balance, Currency
                    FROM invoiceBalances
                    WHERE Balance < 0
                    UNION ALL
                    SELECT N'accountsPayable', b.GrandTotal, b.Currency
                    FROM purchases.Bills b
                    WHERE b.Status = N'Issued'
                        AND (@branch = N'' OR b.Branch = @branch)
                    UNION ALL
                    SELECT N'accountsPayable', -p.Amount, p.Currency
                    FROM purchases.PaymentsMade p
                    WHERE p.Status = N'Recorded'
                        AND (@branch = N'' OR p.Branch = @branch)
                    UNION ALL
                    SELECT N'accountsPayable', -a.Amount, b.Currency
                    FROM purchases.VendorCreditApplications a
                    INNER JOIN purchases.Bills b ON b.Id = a.BillId
                    WHERE a.Status = N'Applied'
                        AND b.Status = N'Issued'
                        AND (@branch = N'' OR b.Branch = @branch)
                ) dashboardAmounts";

            var rows = await _context.Database
                .SqlQueryRaw<DashboardAmountRowDto>(
                    sql,
                    new SqlParameter("@from", from.ToDateTime(TimeOnly.MinValue)),
                    new SqlParameter("@to", to.ToDateTime(TimeOnly.MaxValue)),
                    new SqlParameter("@branch", branch))
                .ToListAsync(cancellationToken);

            var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["revenue"] = 0,
                ["expenses"] = 0,
                ["received"] = 0,
                ["paid"] = 0,
                ["accountsReceivable"] = 0,
                ["customerCredits"] = 0,
                ["accountsPayable"] = 0
            };
            var warnings = new List<string>();

            foreach (var group in rows
                .GroupBy(row => new
                {
                    row.Metric,
                    Currency = row.Currency.ToUpperInvariant()
                }))
            {
                try
                {
                    var rate = await _exchangeRates.GetRateAsync(
                        group.Key.Currency,
                        currency,
                        to,
                        cancellationToken);

                    totals[group.Key.Metric] += group.Sum(row => row.Amount) * rate.Rate;
                }
                catch (Exception exception) when (
                    exception is ValidationException or HttpRequestException or OperationCanceledException)
                {
                    if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    _logger.LogWarning(
                        exception,
                        "Dashboard conversion rate unavailable for {SourceCurrency} to {TargetCurrency}.",
                        group.Key.Currency,
                        currency);

                    warnings.Add(
                        $"{group.Key.Currency} totals could not be converted to {currency}; those amounts are excluded.");
                }
            }

            var customerCount = await _context.Database
                .SqlQueryRaw<int>(@"
                    SELECT COUNT(*) AS Value
                    FROM dbo.ClientMaster
                    WHERE IsActive = 1
                        AND (@branch = N'' OR Branch = @branch)",
                    new SqlParameter("@branch", branch))
                .SingleAsync(cancellationToken);

            var vendorCount = await _context.Database
                .SqlQueryRaw<int>(@"
                    SELECT COUNT(*) AS Value
                    FROM purchases.VendorMaster
                    WHERE IsActive = 1
                        AND (@branch = N'' OR Branch = @branch)",
                    new SqlParameter("@branch", branch))
                .SingleAsync(cancellationToken);

            decimal rateToInr = 1m;
            var rateDate = "INR base currency";
            var rateSource = "Same currency";
            DateTimeOffset? ratePublishedAt = null;
            var rateWarning = warnings.Count == 0
                ? null
                : string.Join(" ", warnings.Distinct());

            try
            {
                var displayRate = await _exchangeRates.GetRateAsync(
                    currency,
                    "INR",
                    to,
                    cancellationToken);

                rateToInr = displayRate.Rate;
                rateDate = displayRate.RateDate.ToString("yyyy-MM-dd");
                rateSource = displayRate.Source;
                ratePublishedAt = displayRate.RetrievedAtUtc;
            }
            catch (Exception exception) when (
                exception is ValidationException or HttpRequestException or OperationCanceledException)
            {
                if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                rateWarning ??= $"The {currency} reference rate is currently unavailable.";
            }

            return new DashboardSummaryDto
            {
                From = from,
                To = to,
                Branch = string.IsNullOrWhiteSpace(branch) ? "All branches" : branch,
                Currency = currency,
                Revenue = totals["revenue"],
                Expenses = totals["expenses"],
                Received = totals["received"],
                Paid = totals["paid"],
                AccountsReceivable = Math.Max(0, totals["accountsReceivable"]),
                CustomerCredits = totals["customerCredits"],
                AccountsPayable = Math.Max(0, totals["accountsPayable"]),
                Customers = customerCount,
                Vendors = vendorCount,
                RateToInr = rateToInr,
                RateDate = rateDate,
                RateSource = rateSource,
                RatePublishedAt = ratePublishedAt,
                RateWarning = rateWarning
            };
        }

        public async Task<List<DashboardActivityDto>> GetActivityAsync(
            CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT TOP (10) Id, EntityType, Action, Actor, At
                FROM
                (
                    SELECT CONCAT(N'payment-', p.Id) AS Id,
                        N'Payment received' AS EntityType,
                        CONCAT(N'Recorded ', p.Amount, N' ', p.Currency) AS Action,
                        i.ClientName AS Actor,
                        CAST(p.PaymentDate AS datetime2) AS At
                    FROM sales.SalesInvoicePayments p
                    INNER JOIN sales.SalesInvoices i ON i.Id = p.SalesInvoiceId
                    WHERE p.Status = N'Recorded' AND i.Status = N'Issued'
                    UNION ALL
                    SELECT CONCAT(N'expense-', e.Id), N'Expense',
                        CONCAT(e.Amount, N' ', e.Currency, N' · ', e.Category),
                        e.Supplier, CAST(e.Date AS datetime2)
                    FROM expenses.Expenses e
                    WHERE e.Status = N'Recorded'
                    UNION ALL
                    SELECT CONCAT(N'bill-', b.Id), N'Purchase bill',
                        CONCAT(N'Issued ', b.GrandTotal, N' ', b.Currency),
                        b.VendorName, CAST(b.BillDate AS datetime2)
                    FROM purchases.Bills b
                    WHERE b.Status = N'Issued'
                    UNION ALL
                    SELECT CONCAT(N'supplier-payment-', p.Id), N'Payment made',
                        CONCAT(N'Paid ', p.Amount, N' ', p.Currency),
                        p.VendorName, CAST(p.PaymentDate AS datetime2)
                    FROM purchases.PaymentsMade p
                    WHERE p.Status = N'Recorded'
                ) recentActivity
                ORDER BY At DESC, Id DESC";

            return await _context.Database
                .SqlQueryRaw<DashboardActivityDto>(sql)
                .ToListAsync(cancellationToken);
        }
    }
}
