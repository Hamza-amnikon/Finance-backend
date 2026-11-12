using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/trial-balance")]
    public class TrialBalanceController
        : ControllerBase
    {
        private readonly ITrialBalanceService
            _service;

        private readonly ILogger<TrialBalanceController>
            _logger;

        public TrialBalanceController(
            ITrialBalanceService service,
            ILogger<TrialBalanceController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] TrialBalanceQueryDto query,
            CancellationToken cancellationToken)
        {
            try
            {
                var result =
                    await _service.GetAsync(
                        query,
                        cancellationToken);

                return Ok(result);
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
                    message =
                        exception.Message
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Trial Balance database request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status503ServiceUnavailable,
                    title:
                        "Accounting database is unavailable.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Trial Balance request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Trial Balance request failed.");
            }
        }
    }
}