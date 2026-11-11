using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesEstimateService.API.Services;

namespace SalesEstimateService.API.Controllers
{
    [ApiController]
    [Route("api/sales-estimates/tax-settings")]
    public class SalesEstimateTaxSettingsController : ControllerBase
    {
        private readonly SalesEstimateTaxSettingsService _tax;

        public SalesEstimateTaxSettingsController(SalesEstimateTaxSettingsService tax)
        {
            _tax = tax;
        }

        [HttpGet]
        public async Task<IActionResult> Choices(
            [FromQuery] string branch,
            [FromQuery] DateOnly estimateDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _tax.ChoicesAsync(branch, estimateDate, cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                return Problem(statusCode: 503, title: "Tax Settings database setup is pending.");
            }
        }
    }
}
