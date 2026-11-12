using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/customer-credits")]
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
                return Ok(await _service.GetAsync(
                    clientId,
                    branch,
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
