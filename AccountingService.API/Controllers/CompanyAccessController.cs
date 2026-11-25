using AccountingService.API.Data;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Http;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/accounts/company-access")]
    public sealed class CompanyAccessController(
        AccountingDbContext database,
        IAccountingPageAccess permissions,
        ILogger<CompanyAccessController> logger) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string route,
            CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";

            try
            {
                var codes = await database.Database.SqlQuery<string>($"""
                    SELECT TOP (1) WITH TIES PermissionCode AS Value
                    FROM security.Permissions
                    WHERE Route = {route} OR (
                        Route <> N'/' AND Route <> N'' AND
                        LEFT({route}, LEN(Route) + 1) = Route + N'/'
                    )
                    ORDER BY LEN(Route) DESC
                    """).ToListAsync(cancellationToken);

                var permissionCode = codes.Count == 1 ? codes[0] : string.Empty;
                var access = await permissions.GetAsync(Request, permissionCode, cancellationToken);
                if (access.Status != 200)
                {
                    return StatusCode(access.Status);
                }

                if (!access.IsAdmin && codes.Count != 1)
                {
                    return StatusCode(403, new { message = "No company access is configured for this page." });
                }

                var branches = await database.Database.SqlQuery<CompanyBranch>($"""
                    SELECT b.Id, b.Name, b.IsActive, b.AccountingCompanyId
                    FROM amnikon.Branches b
                    WHERE NOT EXISTS (
                        SELECT 1 FROM amnikon.Branches other
                        WHERE other.Name = b.Name AND other.Id <> b.Id
                    )
                    """).ToListAsync(cancellationToken);

                return Ok(new
                {
                    access.IsAdmin,
                    access.CompanyIds,
                    access.CreatableCompanyIds,
                    access.EditableCompanyIds,
                    access.DeletableCompanyIds,
                    access.IssuableCompanyIds,
                    access.ApprovableCompanyIds,
                    access.CancellableCompanyIds,
                    access.ExportableCompanyIds,
                    access.PostableCompanyIds,
                    access.VoidableCompanyIds,
                    access.ReversibleCompanyIds,
                    access.ReconcilableCompanyIds,
                    actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    branches = branches.Where(branch => access.IsAdmin ||
                        (branch.AccountingCompanyId is int id && access.Allows(id)))
                });
            }
            catch (Exception exception) when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Company access verification failed.");
                return StatusCode(503, new { message = "Company access could not be verified." });
            }
        }

        public sealed class CompanyBranch
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public int? AccountingCompanyId { get; set; }
        }
    }
}
