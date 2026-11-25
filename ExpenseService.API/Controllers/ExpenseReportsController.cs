using SparkFinance.Http;
using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SparkFinance.Database;

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
                return BadRequest(new
                {
                    message = "Choose a valid date range of up to ten years."
                });
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
                    FROM amnikon.Branches b
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
            const string mySql = @"
                WITH billBalances AS (
                    SELECT b.Id, b.BillNumber, b.VendorId, b.VendorName, b.Branch, b.Currency,
                        b.BillDate, b.DueDate, b.GrandTotal, b.TaxTotal,
                        COALESCE((SELECT SUM(p.Amount) FROM purchases__PaymentsMade p
                            WHERE p.BillId=b.Id AND p.Status='Recorded' AND p.PaymentDate<=@to),0) AS Paid,
                        COALESCE((SELECT SUM(a.Amount) FROM purchases__VendorCreditApplications a
                            WHERE a.BillId=b.Id AND a.Status='Applied' AND a.ApplicationDate<=@to),0) AS Applied
                    FROM purchases__Bills b WHERE b.Status='Issued' AND b.BillDate BETWEEN @from AND @to
                )
                SELECT Id, 'Bill' AS Type, COALESCE(BillNumber,'') AS Number,
                    CAST(VendorId AS CHAR) AS PartyKey, VendorName AS PartyName, Branch,
                    'Bills (uncategorized)' AS Category, Currency, BillDate AS Date, DueDate,
                    GrandTotal-TaxTotal AS Net, TaxTotal AS Tax, GrandTotal AS Total,
                    GREATEST(GrandTotal-Paid-Applied,0) AS Balance,
                    CASE WHEN DueDate<@to THEN GREATEST(GrandTotal-Paid-Applied,0) ELSE 0 END AS Overdue,
                    CASE WHEN GrandTotal-Paid-Applied<=0 THEN 'Settled'
                         WHEN Paid+Applied>0 THEN 'Partially Settled' ELSE 'Unpaid' END AS Status
                FROM billBalances
                UNION ALL
                SELECT v.Id,'VendorCredit',COALESCE(v.VendorCreditNumber,''),CAST(v.VendorId AS CHAR),
                    v.VendorName,v.Branch,'Bills (uncategorized)',v.Currency,v.CreditDate,NULL,
                    -(v.GrandTotal-v.TaxTotal),-v.TaxTotal,-v.GrandTotal,0,0,'Issued'
                FROM purchases__VendorCredits v WHERE v.Status='Issued' AND v.CreditDate BETWEEN @from AND @to
                UNION ALL
                SELECT e.Id,'Expense',CASE WHEN e.Reference='' THEN CONCAT('EXP-',e.Id) ELSE e.Reference END,
                    CONCAT('expense:',e.CompanyId,':',e.Supplier),e.Supplier,
                    CASE WHEN (SELECT COUNT(*) FROM amnikon__Branches b
                        WHERE b.Name=c.Name OR b.CompanyName=c.Name OR INSTR(c.Name,b.Name)>0)=1
                        THEN (SELECT MIN(b.Name) FROM amnikon__Branches b
                            WHERE b.Name=c.Name OR b.CompanyName=c.Name OR INSTR(c.Name,b.Name)>0)
                        ELSE 'Unassigned' END,
                    CASE WHEN e.Category='' THEN 'Uncategorized' ELSE e.Category END,
                    e.Currency,e.Date,NULL,e.Amount,0,e.Amount,0,0,'Recorded'
                FROM expenses__Expenses e INNER JOIN expenses__Companies c ON c.Id=e.CompanyId
                WHERE e.Status='Recorded' AND e.Date BETWEEN @from AND @to
                UNION ALL
                SELECT p.Id,'Payment',p.PaymentNumber,CAST(p.VendorId AS CHAR),p.VendorName,
                    p.Branch,'Bill payments',p.Currency,p.PaymentDate,NULL,0,0,p.Amount,0,0,'Recorded'
                FROM purchases__PaymentsMade p INNER JOIN purchases__Bills b ON b.Id=p.BillId AND b.Status='Issued'
                WHERE p.Status='Recorded' AND p.PaymentDate BETWEEN @from AND @to
                UNION ALL
                SELECT c.Id,'Claim',c.Reference,CONCAT('claim:',c.CreatedBy),c.Employee,
                    c.Branch,c.Category,c.Currency,c.ApprovedDate,NULL,c.Amount,0,c.Amount,
                    CASE WHEN c.PaidDate<=@to THEN 0 ELSE c.Amount END,0,
                    CASE WHEN c.PaidDate<=@to THEN 'Paid' ELSE 'Approved' END
                FROM expenses__ExpenseClaims c
                WHERE c.Status IN ('Approved','Paid') AND c.ApprovedDate BETWEEN @from AND @to
                UNION ALL
                SELECT c.Id,'ClaimPayment',c.PaymentReference,CONCAT('claim:',c.CreatedBy),c.Employee,
                    c.Branch,c.Category,c.Currency,c.PaidDate,NULL,0,0,c.Amount,0,0,'Paid'
                FROM expenses__ExpenseClaims c WHERE c.Status='Paid' AND c.PaidDate BETWEEN @from AND @to
                ";
            try
            {
                var rows = _context.Database.IsFinanceMySql()
                    ? await LoadMySqlRowsAsync(mySql, fromDate, toDate, cancellationToken)
                    : await _context.Database.SqlQueryRaw<ExpenseReportRowDto>(sql,
                        new SqlParameter("@from", fromDate.ToDateTime(TimeOnly.MinValue)),
                        new SqlParameter("@to", toDate.ToDateTime(TimeOnly.MinValue)))
                        .ToListAsync(cancellationToken);
                return Ok(new
                {
                    fromDate,
                    toDate,
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
                _logger.LogError(exception, "Expense report database request failed.");
                return Problem(statusCode: 503, title: "Expense report data is unavailable. Check the finance database.");
            }
        }

        private async Task<List<ExpenseReportRowDto>> LoadMySqlRowsAsync(
            string sql, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        {
            var rows = new List<ExpenseReportRowDto>();
            await _context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                foreach (var section in sql.Split("UNION ALL", StringSplitOptions.None))
                {
                    await using var command = _context.Database.GetDbConnection().CreateCommand();
                    command.CommandText = section;
                    command.Parameters.Add(new MySqlParameter("@from", fromDate.ToDateTime(TimeOnly.MinValue)));
                    command.Parameters.Add(new MySqlParameter("@to", toDate.ToDateTime(TimeOnly.MinValue)));
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        string Text(int index) => reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index)) ?? "";
                        decimal Amount(int index) => reader.IsDBNull(index) ? 0 : Convert.ToDecimal(reader.GetValue(index));
                        rows.Add(new ExpenseReportRowDto
                        {
                            Id = reader.GetInt32(0), Type = Text(1), Number = Text(2), PartyKey = Text(3),
                            PartyName = Text(4), Branch = Text(5), Category = Text(6), Currency = Text(7),
                            Date = DateOnly.FromDateTime(reader.GetDateTime(8)),
                            DueDate = reader.IsDBNull(9) ? null : DateOnly.FromDateTime(reader.GetDateTime(9)),
                            Net = Amount(10), Tax = Amount(11), Total = Amount(12), Balance = Amount(13),
                            Overdue = Amount(14), Status = Text(15)
                        });
                    }
                }
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
            return rows.OrderByDescending(row => row.Date).ThenBy(row => row.Type).ThenBy(row => row.Id).ToList();
        }
    }
}
