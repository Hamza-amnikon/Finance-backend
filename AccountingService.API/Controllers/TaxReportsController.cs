using System.ComponentModel.DataAnnotations;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/tax-reports")]
    public class TaxReportsController : ControllerBase
    {
        private readonly ITaxReportService _service;
        private readonly ILogger<TaxReportsController> _logger;

        public TaxReportsController(
            ITaxReportService service,
            ILogger<TaxReportsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int companyId,
            [FromQuery] DateOnly fromDate,
            [FromQuery] DateOnly toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.GetAsync(
                    companyId, fromDate, toDate, cancellationToken));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
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
                _logger.LogError(exception, "Tax Reports database request failed.");
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Accounting database is unavailable.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Tax Reports request failed.");
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Tax Reports request failed.");
            }
        }
    }
}
