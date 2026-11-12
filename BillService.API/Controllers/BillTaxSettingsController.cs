using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using BillService.API.Services;

namespace BillService.API.Controllers
{
    [ApiController]
    [Route("api/bills/tax-settings")]
    public class BillTaxSettingsController : ControllerBase
    {
        private readonly BillTaxSettingsService _tax;

        public BillTaxSettingsController(BillTaxSettingsService tax)
        {
            _tax = tax;
        }

        [HttpGet]
        public async Task<IActionResult> Choices(
            [FromQuery] string branch,
            [FromQuery] DateOnly billDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _tax.ChoicesAsync(branch, billDate, cancellationToken));
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
