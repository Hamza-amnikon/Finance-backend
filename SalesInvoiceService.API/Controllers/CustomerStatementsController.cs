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
            return Ok(await _service.GetClientsAsync(
                cancellationToken));
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
                return Ok(await _service.GetAsync(
                    clientId,
                    branch,
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
