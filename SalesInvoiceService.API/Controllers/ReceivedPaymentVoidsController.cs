using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-invoices/payments/{paymentId:int:min(1)}/void")]
    public class ReceivedPaymentVoidsController : ControllerBase
    {
        private readonly ReceivedPaymentVoidService _service;

        public ReceivedPaymentVoidsController(ReceivedPaymentVoidService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Void(
            int paymentId,
            VoidReceivedPaymentRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var payment = await _service.VoidAsync(paymentId, request.Reason, cancellationToken);
                return Ok(new
                {
                    payment.Id,
                    payment.Status,
                    payment.VoidedDate,
                    payment.VoidReason
                });
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

    public class VoidReceivedPaymentRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }
}
