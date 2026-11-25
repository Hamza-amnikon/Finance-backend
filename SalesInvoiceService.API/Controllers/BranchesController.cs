using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/branches")]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchService _service;

        public BranchesController(IBranchService service)
        {
            _service = service;
        }

        [HttpGet("{id:int}/invoice-profile")]
        public async Task<IActionResult> GetInvoiceProfile(
            int id,
            [FromServices] FinanceCompanyAccess access,
            CancellationToken cancellationToken)
        {
            try
            {
                var scope = await access.GetAsync(Request, "/settings/organization", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var profile = await _service.GetInvoiceProfileAsync(
                    id,
                    cancellationToken);

                if (profile != null && !scope.Allows(profile.BranchName))
                {
                    return StatusCode(403);
                }

                return profile == null
                    ? NotFound(new
                    {
                        message = "Active branch not found."
                    })
                    : Ok(profile);
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }

        [HttpPut("{id:int}/invoice-profile")]
        public async Task<IActionResult> UpdateInvoiceProfile(
            int id,
            [FromServices] FinanceCompanyAccess access,
            [FromBody] UpdateBranchInvoiceProfileDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var scope = await access.GetAsync(Request, "/settings/organization", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var existing = await _service.GetInvoiceProfileAsync(id, cancellationToken);
                if (existing == null)
                {
                    return NotFound(new { message = "Active branch not found." });
                }

                if (!scope.AllowsAction(existing.BranchName, scope.EditableBranches))
                {
                    return StatusCode(403);
                }

                var profile = await _service.UpdateInvoiceProfileAsync(
                    id,
                    dto,
                    cancellationToken);

                return profile == null
                    ? NotFound(new
                    {
                        message = "Active branch not found."
                    })
                    : Ok(profile);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "Branch details changed since you opened them. Reload the branch before saving."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetActive(
            CancellationToken cancellationToken)
        {
            try
            {
                var branches = await _service.GetActiveAsync(
                    cancellationToken);

                return Ok(branches.Select(branch => new
                {
                    branch.Id,
                    branch.Name,
                    branch.CountryCode,
                    branch.AccountingCompanyId,
                    branch.IsActive
                }));
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }
    }
}
