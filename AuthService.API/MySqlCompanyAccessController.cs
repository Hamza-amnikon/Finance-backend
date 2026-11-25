using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Authentication;
using RoleService.API.Data;
using SparkFinance.Http;

namespace AuthService.API;

[ApiController]
[Authorize(AuthenticationSchemes = FinanceAuthenticationSchemes.MicrosoftSession)]
[Route("api/auth/company-access")]
public sealed class MySqlCompanyAccessController(RoleDbContext database, ILogger<MySqlCompanyAccessController> logger)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery, Required, MaxLength(300)] string route, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!database.IsMySql)
        {
            return NotFound();
        }

        if (!int.TryParse(User.FindFirstValue("role_id"), out var roleId) || roleId <= 0 ||
            !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) || actorId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var isAdmin = User.IsInRole(FinanceRoleNames.Admin);
            var grants = new List<CompanyGrant>();
            if (!isAdmin)
            {
                var pages = await database.Database.SqlQuery<PageMatch>($"""
                    SELECT PermissionCode, CHAR_LENGTH(Route) AS RouteLength FROM security__Permissions
                    WHERE Route = {route} OR (Route NOT IN ('/', '') AND LEFT({route}, CHAR_LENGTH(Route) + 1) = CONCAT(Route, '/'))
                    ORDER BY CHAR_LENGTH(Route) DESC LIMIT 2
                    """).ToListAsync(cancellationToken);
                if (pages.Count == 0 || (pages.Count == 2 && pages[0].RouteLength == pages[1].RouteLength))
                {
                    return Forbid(FinanceAuthenticationSchemes.MicrosoftSession);
                }

                grants = await database.Database.SqlQuery<CompanyGrant>($"""
                    SELECT rp.CompanyId, rp.CanCreate, rp.CanEdit, rp.CanDelete, rp.CanIssue,
                        rp.CanApprove, rp.CanCancel, rp.CanExport, rp.CanPost, rp.CanVoid,
                        rp.CanReverse, rp.CanReconcile
                    FROM security__RolePermissions rp
                    INNER JOIN security__Permissions p ON p.PermissionId = rp.PermissionId
                    WHERE rp.RoleId = {roleId} AND rp.CompanyId > 0 AND rp.CanView = 1
                        AND p.Status = 'Active' AND p.PermissionCode = {pages[0].PermissionCode}
                    """).ToListAsync(cancellationToken);
            }

            var allowed = grants.Select(grant => grant.CompanyId).ToHashSet();
            var branches = await database.Database.SqlQueryRaw<CompanyBranch>("""
                SELECT b.Id, b.Name, b.IsActive, b.AccountingCompanyId
                FROM amnikon__Branches b WHERE NOT EXISTS (
                    SELECT 1 FROM amnikon__Branches other WHERE other.Name = b.Name AND other.Id <> b.Id
                )
                """).ToListAsync(cancellationToken);

            return Ok(new
            {
                isAdmin,
                companyIds = allowed,
                creatableCompanyIds = Ids(grants, grant => grant.CanCreate),
                editableCompanyIds = Ids(grants, grant => grant.CanEdit),
                deletableCompanyIds = Ids(grants, grant => grant.CanDelete),
                issuableCompanyIds = Ids(grants, grant => grant.CanIssue),
                approvableCompanyIds = Ids(grants, grant => grant.CanApprove),
                cancellableCompanyIds = Ids(grants, grant => grant.CanCancel),
                exportableCompanyIds = Ids(grants, grant => grant.CanExport),
                postableCompanyIds = Ids(grants, grant => grant.CanPost),
                voidableCompanyIds = Ids(grants, grant => grant.CanVoid),
                reversibleCompanyIds = Ids(grants, grant => grant.CanReverse),
                reconcilableCompanyIds = Ids(grants, grant => grant.CanReconcile),
                actorId,
                branches = branches.Where(branch => isAdmin ||
                    (branch.AccountingCompanyId is int id && allowed.Contains(id)))
            });
        }
        catch (Exception exception) when (RequestCancellation.IsCancellation(exception, cancellationToken))
        {
            return StatusCode(499);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "MySQL company access lookup failed.");
            return StatusCode(503, new { message = "Company access could not be verified." });
        }
    }

    private static int[] Ids(List<CompanyGrant> grants, Func<CompanyGrant, bool> allows) =>
        grants.Where(allows).Select(grant => grant.CompanyId).Distinct().ToArray();

    public sealed record PageMatch(string PermissionCode, int RouteLength);
    public sealed record CompanyBranch(int Id, string Name, bool IsActive, int? AccountingCompanyId);
    public sealed record CompanyGrant(int CompanyId, bool CanCreate, bool CanEdit, bool CanDelete,
        bool CanIssue, bool CanApprove, bool CanCancel, bool CanExport, bool CanPost,
        bool CanVoid, bool CanReverse, bool CanReconcile);
}
