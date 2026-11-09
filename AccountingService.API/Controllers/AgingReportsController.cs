using AccountingService.API.Data;
using AccountingService.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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
                return BadRequest(new { message = "Choose an As of date on or before today." });
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
                        WHERE SalesInvoiceId = i.Id AND PaymentDate <= @asOf) p
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
            try
            {
                var rows = await _context.Database.SqlQueryRaw<AgingReportRowDto>(
                    sql,
                    new SqlParameter("@asOf", asOf.ToDateTime(TimeOnly.MinValue)),
                    new SqlParameter("@nextDay", asOf.AddDays(1).ToDateTime(TimeOnly.MinValue))
                ).ToListAsync(cancellationToken);
                return Ok(new { asOf, rows });
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Aging report database request failed.");
                return Problem(statusCode: 503, title: "Aging report data is unavailable. Check the finance database.");
            }
        }
    }
}
