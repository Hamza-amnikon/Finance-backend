using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/accounting-postings/purchase-bills")]
    public class PurchaseBillReversalsController : ControllerBase
    {
        private readonly PurchaseBillReversalService _service;

        public PurchaseBillReversalsController(PurchaseBillReversalService service)
        {
            _service = service;
        }

        [HttpPost("{billId:int:min(1)}/reverse")]
        public async Task<IActionResult> Reverse(int billId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.ReverseAsync(billId, cancellationToken));
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
        }
    }
}
