using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.DTOs;
using RoleService.API.Services;

namespace RoleService.API.Controllers;

[ApiController]
[Route("api/auth/payroll-expense-preview")]
[Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.BrowserSession)]
public sealed class PayrollExpensePreviewController(
    RoleDbContext db,
    PayrollExpenseClient payroll) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PayrollExpensePreviewDto>> GetPreview(
        [FromQuery, Range(1, int.MaxValue)] int companyId,
        [FromQuery, Range(2000, 9999)] int year,
        [FromQuery, Range(1, 12)] int month,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        if (!int.TryParse(User.FindFirstValue("role_id"), NumberStyles.None,
                CultureInfo.InvariantCulture, out var roleId) || roleId <= 0)
        {
            return Forbid(FinanceAuthenticationSchemes.BrowserSession);
        }

        var grants = await db.Database.SqlQuery<int>($"""
            SELECT TOP (1) 1 AS [Value]
            FROM [security].[RolePermissions] AS rp
            INNER JOIN [security].[Permissions] AS p ON p.PermissionId = rp.PermissionId
            WHERE rp.RoleId = {roleId} AND rp.CompanyId = {companyId}
                AND rp.CanView = 1 AND rp.CanCreate = 1
                AND p.[Status] = 'Active' AND p.[Route] = '/expenses'
            """).ToListAsync(cancellationToken);

        if (grants.Count == 0)
        {
            return Forbid(FinanceAuthenticationSchemes.BrowserSession);
        }

        var (status, preview) = await payroll.GetSummaryAsync(companyId, year, month, cancellationToken);
        if (status == HttpStatusCode.OK)
        {
            return Ok(preview);
        }

        return StatusCode((int)status, new
        {
            message = status switch
            {
                HttpStatusCode.NotFound => "No completed payroll is available for this company and month.",
                HttpStatusCode.Conflict => "The payroll run needs review before it can be used for an expense.",
                HttpStatusCode.ServiceUnavailable => "Payroll is not configured for this company. Ask your administrator to check the SPARK payroll mapping and connection.",
                _ => "Payroll could not be loaded. Please try again."
            }
        });
    }
}
