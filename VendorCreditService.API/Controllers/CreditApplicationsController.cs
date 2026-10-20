using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VendorCreditService.API.DTOs;
using VendorCreditService.API.Services;

namespace VendorCreditService.API.Controllers
{
    [ApiController]
    [Route("api/vendor-credits/{creditId:int}/applications")]
    public class CreditApplicationsController : ControllerBase
    {
        private readonly ICreditApplicationService _service;
        private readonly ILogger<CreditApplicationsController> _logger;

        public CreditApplicationsController(
            ICreditApplicationService service,
            ILogger<CreditApplicationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("bills")]
        public Task<IActionResult> Bills(
            [Range(1, int.MaxValue)] int creditId,
            CancellationToken cancellationToken,
            [FromQuery, MaxLength(200)] string? search = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 50)
        {
            return HandleAsync(async () =>
            {
                var (data, total) = await _service.GetBillsAsync(
                    creditId, search, page, pageSize, cancellationToken);

                return Ok(new
                {
                    data,
                    total,
                    page,
                    pageSize
                });
            });
        }

        [HttpPost]
        public Task<IActionResult> Apply(
            [Range(1, int.MaxValue)] int creditId,
            [FromBody] ApplyVendorCreditDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var application = await _service.ApplyAsync(
                    creditId, dto, cancellationToken);

                return Ok(application);
            });
        }

        [HttpPost("{applicationId:int}/void")]
        public Task<IActionResult> Void(
            [Range(1, int.MaxValue)] int creditId,
            [Range(1, int.MaxValue)] int applicationId,
            [FromBody] VoidCreditApplicationDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var application = await _service.VoidAsync(
                    creditId, applicationId, dto, cancellationToken);

                if (application == null)
                {
                    return NotFound(new
                    {
                        message = "Credit application not found."
                    });
                }

                return Ok(application);
            });
        }

        private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "The credit or bill changed. Reload before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Unable to save credit application.");
                return Problem(statusCode: 500, title: "Unable to save credit application.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Credit application database unavailable.");
                return Problem(statusCode: 503, title: "Database is temporarily unavailable.");
            }
        }
    }
}
