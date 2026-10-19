using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-invoices/payments")]
    public class SalesPaymentsController : ControllerBase
    {
        private readonly ISalesInvoicePaymentService _paymentService;
        private readonly ILogger<SalesPaymentsController> _logger;

        public SalesPaymentsController(
            ISalesInvoicePaymentService paymentService,
            ILogger<SalesPaymentsController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] Guid? clientId = null,
            [FromQuery] string? branch = null,
            [FromQuery] string? currency = null,
            [FromQuery] string? paymentMode = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _paymentService.GetPagedAsync(
                    page,
                    pageSize,
                    search,
                    fromDate,
                    toDate,
                    clientId,
                    branch,
                    currency,
                    paymentMode,
                    cancellationToken);

                return Ok(new
                {
                    data = result.Items,
                    total = result.Total,
                    page,
                    pageSize
                });
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
            catch (SqlException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to load received sales payments.");

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "Payments could not be loaded. Please try again."
                    });
            }
        }
    }
}