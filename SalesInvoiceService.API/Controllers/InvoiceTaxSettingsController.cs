using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-invoices/tax-settings")]
    public class InvoiceTaxSettingsController : ControllerBase
    {
        private readonly InvoiceTaxSettingsService _tax;

        public InvoiceTaxSettingsController(InvoiceTaxSettingsService tax)
        {
            _tax = tax;
        }

        [HttpGet]
        public async Task<IActionResult> Choices(
            [FromQuery] string branch,
            [FromQuery] DateOnly invoiceDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _tax.ChoicesAsync(branch, invoiceDate, cancellationToken));
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
