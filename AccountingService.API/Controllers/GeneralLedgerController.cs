using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(GeneralLedgerAccessFilter))]
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

        [HttpGet("companies")]
        public async Task<IActionResult> Companies(
            [FromServices] IAccountService accounts,
            CancellationToken cancellationToken)
        {
            var access = (LedgerAccess)HttpContext.Items[typeof(LedgerAccess)]!;
            var companies = await accounts.GetCompaniesAsync(cancellationToken);
            return Ok(companies.Where(company => access.Allows(company.Id)).Select(company => new
            {
                company.Id,
                company.Name,
                company.Country,
                company.Currency
            }));
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> Accounts(
            [FromQuery] AccountQueryDto query,
            [FromServices] IAccountService accounts,
            CancellationToken cancellationToken)
        {
            return Ok(await accounts.GetPagedAsync(query, cancellationToken));
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