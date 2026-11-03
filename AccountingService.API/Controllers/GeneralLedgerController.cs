using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/general-ledger")]
    public class GeneralLedgerController
        : ControllerBase
    {
        private readonly IGeneralLedgerService
            _service;

        private readonly ILogger<GeneralLedgerController>
            _logger;

        public GeneralLedgerController(
            IGeneralLedgerService service,
            ILogger<GeneralLedgerController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] GeneralLedgerQueryDto query,
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
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
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
                    "General Ledger database request failed.");

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
                    "General Ledger request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "General Ledger request failed.");
            }
        }
    }
}