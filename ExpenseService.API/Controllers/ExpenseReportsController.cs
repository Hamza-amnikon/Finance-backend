using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Controllers
{
    [ApiController]
    [Route("api/expense-reports")]
    public class ExpenseReportsController : ControllerBase
    {
        private readonly ExpenseDbContext _context;
        private readonly ILogger<ExpenseReportsController> _logger;

        public ExpenseReportsController(ExpenseDbContext context, ILogger<ExpenseReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateOnly fromDate,
            [FromQuery] DateOnly toDate,
            CancellationToken cancellationToken)
        {
            if (fromDate == default || toDate == default || fromDate > toDate ||
                toDate.DayNumber - fromDate.DayNumber > 3660)
            {
                return BadRequest(new { message = "Choose a valid date range of up to ten years." });
            }

            // Read-only cross-module reporting against the existing shared database.
            // Bill payments and credit applications settle balances; they are not new costs.
            const string sql = @"
                WITH billBalances AS
                (
                    SELECT b.*, COALESCE(p.Paid, 0) AS Paid,
                        COALESCE(a.Applied, 0) AS Applied,
                        CASE WHEN b.GrandTotal - COALESCE(p.Paid, 0) - COALESCE(a.Applied, 0) > 0
                            THEN b.GrandTotal - COALESCE(p.Paid, 0) - COALESCE(a.Applied, 0)
                            ELSE 0 END AS Balance
                    FROM purchases.Bills b
                    OUTER APPLY (SELECT SUM(Amount) AS Paid FROM purchases.PaymentsMade
                        WHERE BillId = b.Id AND Status = N'Recorded' AND PaymentDate <= @to) p
                    OUTER APPLY (SELECT SUM(Amount) AS Applied FROM purchases.VendorCreditApplications
                        WHERE BillId = b.Id AND Status = N'Applied' AND ApplicationDate <= @to) a
                    WHERE b.Status = N'Issued' AND b.BillDate BETWEEN @from AND @to
                )
                SELECT Id, N'Bill' AS Type, COALESCE(BillNumber, N'') AS Number,
                    CONVERT(nvarchar(100), VendorId) AS PartyKey, VendorName AS PartyName,
                    Branch, N'Bills (uncategorized)' AS Category, Currency, BillDate AS Date, DueDate,
                    GrandTotal - TaxTotal AS Net, TaxTotal AS Tax, GrandTotal AS Total, Balance,
                    CASE WHEN DueDate < @to THEN Balance ELSE 0 END AS Overdue,
                    CASE WHEN Balance = 0 THEN N'Settled' WHEN Paid + Applied > 0 THEN N'Partially Settled' ELSE N'Unpaid' END AS Status
                FROM billBalances
                UNION ALL
                SELECT v.Id, N'VendorCredit', COALESCE(v.VendorCreditNumber, N''),
                    CONVERT(nvarchar(100), v.VendorId), v.VendorName, v.Branch,
                    N'Bills (uncategorized)', v.Currency, v.CreditDate, NULL,
                    -(v.GrandTotal - v.TaxTotal), -v.TaxTotal, -v.GrandTotal, 0, 0, N'Issued'
                FROM purchases.VendorCredits v
                WHERE v.Status = N'Issued' AND v.CreditDate BETWEEN @from AND @to
                UNION ALL
                SELECT e.Id, N'Expense', CASE WHEN e.Reference = N'' THEN CONCAT(N'EXP-', e.Id) ELSE e.Reference END,
                    CONCAT(N'expense:', e.CompanyId, N':', e.Supplier), e.Supplier,
                    CASE WHEN m.Matches = 1 THEN m.Branch ELSE N'Unassigned' END,
                    CASE WHEN e.Category = N'' THEN N'Uncategorized' ELSE e.Category END,
                    e.Currency, e.Date, NULL, e.Amount, 0, e.Amount, 0, 0, N'Recorded'
                FROM expenses.Expenses e
                INNER JOIN expenses.Companies c ON c.Id = e.CompanyId
                OUTER APPLY (SELECT COUNT(*) AS Matches, MIN(b.Name) AS Branch
                    FROM sales.BranchMasters b
                    WHERE b.Name = c.Name OR b.CompanyName = c.Name OR CHARINDEX(b.Name, c.Name) > 0) m
                WHERE e.Status = N'Recorded' AND e.Date BETWEEN @from AND @to
                UNION ALL
                SELECT p.Id, N'Payment', p.PaymentNumber, CONVERT(nvarchar(100), p.VendorId),
                    p.VendorName, p.Branch, N'Bill payments', p.Currency, p.PaymentDate, NULL,
                    0, 0, p.Amount, 0, 0, N'Recorded'
                FROM purchases.PaymentsMade p
                INNER JOIN purchases.Bills b ON b.Id = p.BillId AND b.Status = N'Issued'
                WHERE p.Status = N'Recorded' AND p.PaymentDate BETWEEN @from AND @to
                UNION ALL
                SELECT c.Id, N'Claim', c.Reference, CONCAT(N'claim:', c.CreatedBy), c.Employee,
                    c.Branch, c.Category, c.Currency, c.ApprovedDate, NULL, c.Amount, 0, c.Amount,
                    CASE WHEN c.PaidDate <= @to THEN 0 ELSE c.Amount END, 0,
                    CASE WHEN c.PaidDate <= @to THEN N'Paid' ELSE N'Approved' END
                FROM expenses.ExpenseClaims c
                WHERE c.Status IN (N'Approved', N'Paid') AND c.ApprovedDate BETWEEN @from AND @to
                UNION ALL
                SELECT c.Id, N'ClaimPayment', c.PaymentReference, CONCAT(N'claim:', c.CreatedBy), c.Employee,
                    c.Branch, c.Category, c.Currency, c.PaidDate, NULL, 0, 0, c.Amount, 0, 0, N'Paid'
                FROM expenses.ExpenseClaims c
                WHERE c.Status = N'Paid' AND c.PaidDate BETWEEN @from AND @to
                ORDER BY Date DESC, Type, Id";
            try
            {
                var rows = await _context.Database.SqlQueryRaw<ExpenseReportRowDto>(sql,
                    new SqlParameter("@from", fromDate.ToDateTime(TimeOnly.MinValue)),
                    new SqlParameter("@to", toDate.ToDateTime(TimeOnly.MinValue)))
                    .ToListAsync(cancellationToken);
                return Ok(new { fromDate, toDate, rows });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Expense report database request failed.");
                return Problem(statusCode: 503, title: "Expense report data is unavailable. Check the finance database.");
            }
        }
    }
}
