using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-invoices/{salesInvoiceId:int}/payments")]
    public class SalesInvoicePaymentsController : ControllerBase
    {
        private readonly ISalesInvoicePaymentService _paymentService;

        public SalesInvoicePaymentsController(
            ISalesInvoicePaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            int salesInvoiceId,
            [FromBody] CreateSalesInvoicePaymentDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var payment = await _paymentService.CreateAsync(
                    salesInvoiceId,
                    dto,
                    cancellationToken);

                return Ok(payment);
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
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary(
            int salesInvoiceId,
            CancellationToken cancellationToken)
        {
            try
            {
                var summary = await _paymentService.GetSummaryAsync(
                    salesInvoiceId,
                    cancellationToken);

                if (summary == null)
                {
                    return NotFound(new
                    {
                        message = "Invoice not found."
                    });
                }

                return Ok(summary);
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
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