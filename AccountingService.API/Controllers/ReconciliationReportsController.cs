using System.ComponentModel.DataAnnotations;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/reconciliation-reports")]
    public class ReconciliationReportsController : ControllerBase
    {
        private readonly IReconciliationReportService _service;
        private readonly ILogger<ReconciliationReportsController> _logger;
        public ReconciliationReportsController(
            IReconciliationReportService service,
            ILogger<ReconciliationReportsController> logger
        )
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int companyId,
            [FromQuery] DateOnly asOf,
            CancellationToken cancellationToken
        )
        {
            try
            {
                return Ok(await _service.GetAsync(companyId, asOf, cancellationToken));
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Reconciliation report database request failed.");
                return Problem(
                    statusCode: 503,
                    title: "Reconciliation data is unavailable. Check that all finance modules and their database setup are installed."
                );
            }
        }
    }
}
