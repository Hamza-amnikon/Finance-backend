using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PurchaseOrderService.API.Services;

namespace PurchaseOrderService.API.Controllers
{
    [ApiController]
    [Route("api/purchase-orders/tax-settings")]
    public class PurchaseOrderTaxSettingsController : ControllerBase
    {
        private readonly PurchaseOrderTaxSettingsService _tax;

        public PurchaseOrderTaxSettingsController(PurchaseOrderTaxSettingsService tax)
        {
            _tax = tax;
        }

        [HttpGet]
        public async Task<IActionResult> Choices(
            [FromQuery] string branch,
            [FromQuery] DateOnly orderDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _tax.ChoicesAsync(branch, orderDate, cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (Exception cancellationException)
                when (RequestCancellation.IsCancellation(
                    cancellationException,
                    HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                return StatusCode(499);
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                return Problem(statusCode: 503, title: "Tax Settings database setup is pending.");
            }
        }
    }
}
