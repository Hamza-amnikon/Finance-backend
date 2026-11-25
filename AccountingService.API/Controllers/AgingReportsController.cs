using SparkFinance.Http;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Database;
using MySql.Data.MySqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/aging-reports")]
    public class AgingReportsController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly ILogger<AgingReportsController> _logger;
        public AgingReportsController(
            AccountingDbContext context,
            ILogger<AgingReportsController> logger
        )
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateOnly asOf,
            CancellationToken cancellationToken
        )
        {
            if (asOf == default || asOf > DateOnly.FromDateTime(DateTime.Today))
            {
                return BadRequest(new
                {
                    message = "Choose an As of date on or before today."
                });
            }

            const string sql = @"
                WITH balances AS
                (
                    SELECT i.Id, N'Receivable' AS Kind, COALESCE(i.InvoiceNumber, N'') AS Number,
                        CONVERT(nvarchar(100), i.ClientId) AS PartyId, i.ClientName AS PartyName,
                        i.Branch, i.Currency, i.InvoiceDate AS Date, i.DueDate,
                        i.GrandTotal AS Total, COALESCE(p.Paid, 0) AS Paid,
                        COALESCE(c.Credits, 0) + COALESCE(a.Applied, 0) AS Credits,
                        i.GrandTotal - COALESCE(p.Paid, 0) - COALESCE(c.Credits, 0) - COALESCE(a.Applied, 0) AS Balance
                    FROM sales.SalesInvoices i
                    OUTER APPLY (SELECT SUM(Amount) AS Paid FROM sales.SalesInvoicePayments
                        WHERE Status = N'Recorded' AND SalesInvoiceId = i.Id AND PaymentDate <= @asOf) p
                    OUTER APPLY (SELECT SUM(GrandTotal) AS Credits FROM sales.SalesCreditNotes
                        WHERE SalesInvoiceId = i.Id AND Status = N'Issued' AND CreditNoteDate <= @asOf) c
                    OUTER APPLY (SELECT SUM(Amount) AS Applied FROM sales.CustomerCreditApplications
                        WHERE TargetInvoiceId = i.Id AND AppliedAtUtc < @nextDay) a
                    WHERE i.Status = N'Issued' AND i.InvoiceDate <= @asOf
                    UNION ALL
                    SELECT b.Id, N'Payable', COALESCE(b.BillNumber, N''),
                        CONVERT(nvarchar(100), b.VendorId), b.VendorName, b.Branch, b.Currency,
                        b.BillDate, b.DueDate, b.GrandTotal, COALESCE(p.Paid, 0), COALESCE(a.Applied, 0),
                        b.GrandTotal - COALESCE(p.Paid, 0) - COALESCE(a.Applied, 0)
                    FROM purchases.Bills b
                    OUTER APPLY (SELECT SUM(Amount) AS Paid FROM purchases.PaymentsMade
                        WHERE BillId = b.Id AND Status = N'Recorded' AND PaymentDate <= @asOf) p
                    OUTER APPLY (SELECT SUM(Amount) AS Applied FROM purchases.VendorCreditApplications
                        WHERE BillId = b.Id AND Status = N'Applied' AND ApplicationDate <= @asOf) a
                    WHERE b.Status = N'Issued' AND b.BillDate <= @asOf
                )
                SELECT * FROM balances WHERE Balance > 0 ORDER BY DueDate, Kind, Id";
            const string mySql = @"
                WITH balances AS (
                    SELECT i.Id, 'Receivable' AS Kind, COALESCE(i.InvoiceNumber, '') AS Number,
                        CAST(i.ClientId AS CHAR) AS PartyId, i.ClientName AS PartyName,
                        i.Branch, i.Currency, i.InvoiceDate AS Date, i.DueDate,
                        i.GrandTotal AS Total,
                        COALESCE((SELECT SUM(p.Amount) FROM sales__SalesInvoicePayments p
                            WHERE p.Status = 'Recorded' AND p.SalesInvoiceId = i.Id AND p.PaymentDate <= @asOf), 0) AS Paid,
                        COALESCE((SELECT SUM(c.GrandTotal) FROM sales__SalesCreditNotes c
                            WHERE c.SalesInvoiceId = i.Id AND c.Status = 'Issued' AND c.CreditNoteDate <= @asOf), 0)
                        + COALESCE((SELECT SUM(a.Amount) FROM sales__CustomerCreditApplications a
                            WHERE a.TargetInvoiceId = i.Id AND a.AppliedAtUtc < @nextDay), 0) AS Credits
                    FROM sales__SalesInvoices i
                    WHERE i.Status = 'Issued' AND i.InvoiceDate <= @asOf
                    UNION ALL
                    SELECT b.Id, 'Payable', COALESCE(b.BillNumber, ''),
                        CAST(b.VendorId AS CHAR), b.VendorName, b.Branch, b.Currency,
                        b.BillDate, b.DueDate, b.GrandTotal,
                        COALESCE((SELECT SUM(p.Amount) FROM purchases__PaymentsMade p
                            WHERE p.BillId = b.Id AND p.Status = 'Recorded' AND p.PaymentDate <= @asOf), 0),
                        COALESCE((SELECT SUM(a.Amount) FROM purchases__VendorCreditApplications a
                            WHERE a.BillId = b.Id AND a.Status = 'Applied' AND a.ApplicationDate <= @asOf), 0)
                    FROM purchases__Bills b
                    WHERE b.Status = 'Issued' AND b.BillDate <= @asOf
                )
                SELECT *, Total - Paid - Credits AS Balance FROM balances
                WHERE Total - Paid - Credits > 0 ORDER BY DueDate, Kind, Id";
            try
            {
                var rows = _context.Database.IsFinanceMySql()
                    ? (await _context.Database.SqlQueryRaw<MySqlAgingReportRow>(
                        mySql,
                        new MySqlParameter("@asOf", asOf.ToDateTime(TimeOnly.MinValue)),
                        new MySqlParameter("@nextDay", asOf.AddDays(1).ToDateTime(TimeOnly.MinValue)))
                        .ToListAsync(cancellationToken)).Select(row => new AgingReportRowDto
                        {
                            Id = row.Id,
                            Kind = row.Kind,
                            Number = row.Number,
                            PartyId = row.PartyId,
                            PartyName = row.PartyName,
                            Branch = row.Branch,
                            Currency = row.Currency,
                            Date = DateOnly.FromDateTime(row.Date),
                            DueDate = row.DueDate is { } dueDate ? DateOnly.FromDateTime(dueDate) : null,
                            Total = row.Total,
                            Paid = row.Paid,
                            Credits = row.Credits,
                            Balance = row.Balance
                        }).ToList()
                    : await _context.Database.SqlQueryRaw<AgingReportRowDto>(
                        sql,
                        new SqlParameter("@asOf", asOf.ToDateTime(TimeOnly.MinValue)),
                        new SqlParameter("@nextDay", asOf.AddDays(1).ToDateTime(TimeOnly.MinValue)))
                        .ToListAsync(cancellationToken);
                return Ok(new
                {
                    asOf,
                    rows
                });
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Aging report database request failed.");
                return Problem(statusCode: 503, title: "Aging report data is unavailable. Check the finance database.");
            }
        }

        private sealed class MySqlAgingReportRow
        {
            public int Id { get; set; }
            public string Kind { get; set; } = "";
            public string Number { get; set; } = "";
            public string PartyId { get; set; } = "";
            public string PartyName { get; set; } = "";
            public string Branch { get; set; } = "";
            public string Currency { get; set; } = "";
            public DateTime Date { get; set; }
            public DateTime? DueDate { get; set; }
            public decimal Total { get; set; }
            public decimal Paid { get; set; }
            public decimal Credits { get; set; }
            public decimal Balance { get; set; }
        }
    }
}
