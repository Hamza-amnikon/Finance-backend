using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/financial-reports")]
    public class FinancialReportsController : ControllerBase
    {
        private readonly IFinancialReportService _service;
        private readonly ILogger<FinancialReportsController> _logger;

        public FinancialReportsController(
            IFinancialReportService service,
            ILogger<FinancialReportsController> logger)
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
                    companyId,
                    fromDate,
                    toDate,
                    cancellationToken));
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new
                {
                    message = exception.Message
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Financial Reports database request failed.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Accounting database is unavailable.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Financial Reports request failed.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Financial Reports request failed.");
            }
        }
    }
}
