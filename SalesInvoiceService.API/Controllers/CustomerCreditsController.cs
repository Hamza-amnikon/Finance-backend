using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/customer-credits")]
    [ServiceFilter(typeof(SalesCreditAccessFilter))]
    public class CustomerCreditsController : ControllerBase
    {
        private readonly ICustomerCreditService _service;

        public CustomerCreditsController(
            ICustomerCreditService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid clientId,
            [FromQuery] string branch,
            [FromQuery] string currency,
            CancellationToken cancellationToken)
        {
            try
            {
                var scope = await FinanceCompanyAccess.GetMySqlReadScopeAsync(HttpContext, "/sales/credit-notes", cancellationToken);
                if (scope.Status != 200 || !scope.Allows(branch?.Trim()))
                {
                    return StatusCode(scope.Status == 200 ? 403 : scope.Status);
                }

                return Ok(await _service.GetAsync(
                    clientId,
                    branch ?? string.Empty,
                    currency,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
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

        [HttpPost("applications")]
        public async Task<IActionResult> Apply(
            [FromBody] ApplyCustomerCreditDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.ApplyAsync(
                    dto,
                    cancellationToken));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new
                {
                    message = exception.Message
                });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
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
