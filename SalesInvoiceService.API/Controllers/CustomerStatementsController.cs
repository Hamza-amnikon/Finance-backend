using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/customer-statements")]
    public class CustomerStatementsController : ControllerBase
    {
        private readonly ICustomerStatementService _service;

        public CustomerStatementsController(
            ICustomerStatementService service)
        {
            _service = service;
        }

        [HttpGet("clients")]
        public async Task<IActionResult> GetClients(
            CancellationToken cancellationToken)
        {
            var scope = await FinanceCompanyAccess.GetMySqlReadScopeAsync(HttpContext, "/sales/customer-statements", cancellationToken);
            if (scope.Status != 200)
            {
                return StatusCode(scope.Status);
            }

            return Ok(await _service.GetClientsAsync(
                cancellationToken, scope.Branches));
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid clientId,
            [FromQuery] string branch,
            [FromQuery] string currency,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var scope = await FinanceCompanyAccess.GetMySqlReadScopeAsync(HttpContext, "/sales/customer-statements", cancellationToken);
                if (scope.Status != 200 || !scope.Allows(branch?.Trim()))
                {
                    return StatusCode(scope.Status == 200 ? 403 : scope.Status);
                }

                return Ok(await _service.GetAsync(
                    clientId,
                    branch ?? string.Empty,
                    currency,
                    cancellationToken,
                    fromDate,
                    toDate));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }
    }
}
