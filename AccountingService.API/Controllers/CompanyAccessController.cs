using AccountingService.API.Data;
using AccountingService.API.Models;
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
                var matching = database.Set<LedgerPagePermissionLookup>().AsNoTracking()
                    .Where(permission => permission.Route == route ||
                        (permission.Route != "/" && permission.Route != "" &&
                            route.StartsWith(permission.Route + "/")));
                var longestRoute = await matching.Select(permission => (int?)permission.Route.Length)
                    .MaxAsync(cancellationToken);
                var codes = await matching.Where(permission => permission.Route.Length == longestRoute)
                    .Select(permission => permission.PermissionCode)
                    .ToListAsync(cancellationToken);

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

                var branchRows = database.Set<LedgerBranchAccessLookup>().AsNoTracking();
                var branches = await branchRows
                    .Where(branch => !branchRows.Any(other => other.Name == branch.Name && other.Id != branch.Id))
                    .Select(branch => new CompanyBranch
                    {
                        Id = branch.Id,
                        Name = branch.Name,
                        IsActive = branch.IsActive,
                        AccountingCompanyId = branch.AccountingCompanyId
                    })
                    .ToListAsync(cancellationToken);

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
