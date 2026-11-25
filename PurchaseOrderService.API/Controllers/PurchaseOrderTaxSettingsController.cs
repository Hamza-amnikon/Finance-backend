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
        private readonly FinanceCompanyAccess _access;

        public PurchaseOrderTaxSettingsController(
            PurchaseOrderTaxSettingsService tax,
            FinanceCompanyAccess access)
        {
            _tax = tax;
            _access = access;
        }

        [HttpGet]
        public async Task<IActionResult> Choices(
            [FromQuery] string branch,
            [FromQuery] DateOnly orderDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var scope = await _access.GetAsync(Request, "/purchases/orders", cancellationToken);
                Response.Headers.CacheControl = "no-store";
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }
                if (!scope.Allows(branch))
                {
                    return Forbid();
                }

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
