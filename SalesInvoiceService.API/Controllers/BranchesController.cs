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
            CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _service.GetInvoiceProfileAsync(
                    id,
                    cancellationToken);

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
            [FromBody] UpdateBranchInvoiceProfileDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
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
