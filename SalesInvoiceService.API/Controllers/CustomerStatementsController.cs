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
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
        }
    }
}
