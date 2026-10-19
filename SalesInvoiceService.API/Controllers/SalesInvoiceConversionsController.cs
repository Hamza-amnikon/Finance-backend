using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-invoices/from-estimate")]
    public class SalesInvoiceConversionsController : ControllerBase
    {
        private readonly IInvoiceEstimateConversionService _service;

        public SalesInvoiceConversionsController(IInvoiceEstimateConversionService service)
        {
            _service = service;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SalesInvoiceResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(
            [FromBody] CreateInvoiceFromEstimateDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.CreateAsync(dto, cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (OverflowException)
            {
                return BadRequest(new { message = "Invoice amounts exceed the supported range." });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                return StatusCode(503, new { message = "Invoice save failed. Retry the same conversion request." });
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                return StatusCode(503, new { message = "Invoice database is unavailable. Retry the same conversion request." });
            }
            catch (HttpRequestException)
            {
                return StatusCode(503, new { message = "Estimate or client service is unavailable. Retry the same request later." });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(504, new { message = "A related service timed out. Retry the same request later." });
            }
        }
    }
}
