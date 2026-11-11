using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/payment-modes")]
    public class PaymentModesController : ControllerBase
    {
        private readonly IPaymentModeService _service;

        public PaymentModesController(
            IPaymentModeService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetActive(
            CancellationToken cancellationToken)
        {
            try
            {
                var paymentModes = await _service.GetActiveAsync(
                    cancellationToken);

                return Ok(paymentModes);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
                when (cancellationToken.IsCancellationRequested &&
                      exception.Number == 0)
            {
                return StatusCode(499);
            }
        }
    }
}