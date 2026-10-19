using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-credit-notes")]
    public class SalesCreditNotesController : ControllerBase
    {
        private readonly ISalesCreditNoteService _service;
        private readonly ILogger<SalesCreditNotesController> _logger;

        public SalesCreditNotesController(
            ISalesCreditNoteService service,
            ILogger<SalesCreditNotesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] SalesCreditNoteStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            return HandleAsync(async () =>
            {
                var result = await _service.GetPagedAsync(
                    page,
                    pageSize,
                    search,
                    status,
                    cancellationToken);

                return Ok(new
                {
                    data = result.Items,
                    total = result.Total,
                    page,
                    pageSize
                });
            }, cancellationToken);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(
            typeof(SalesCreditNoteResponseDto),
            StatusCodes.Status200OK)]
        public Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var creditNote = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                if (creditNote == null)
                {
                    return NotFound(new
                    {
                        message = "Credit note not found."
                    });
                }

                return Ok(creditNote);
            }, cancellationToken);
        }

        [HttpPost("preview")]
        [ProducesResponseType(
            typeof(SalesCreditNoteResponseDto),
            StatusCodes.Status200OK)]
        public Task<IActionResult> Preview(
            [FromBody] CreateSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var preview = await _service.PreviewAsync(
                    dto,
                    cancellationToken);

                return Ok(preview);
            }, cancellationToken);
        }

        [HttpPost]
        [ProducesResponseType(
            typeof(SalesCreditNoteResponseDto),
            StatusCodes.Status201Created)]
        public Task<IActionResult> Create(
            [FromBody] CreateSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var creditNote = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = creditNote.Id },
                    creditNote);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/issue")]
        [ProducesResponseType(
            typeof(SalesCreditNoteResponseDto),
            StatusCodes.Status200OK)]
        public Task<IActionResult> Issue(
            int id,
            [FromBody] IssueSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var creditNote = await _service.IssueAsync(
                    id,
                    dto,
                    cancellationToken);

                if (creditNote == null)
                {
                    return NotFound(new
                    {
                        message = "Credit note not found."
                    });
                }

                return Ok(creditNote);
            }, cancellationToken);
        }

        private async Task<IActionResult> HandleAsync(
            Func<Task<IActionResult>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "The credit note changed. Reload it before continuing."
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
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Credit note database operation failed.");

                return StatusCode(503, new
                {
                    message =
                        "The database operation could not be completed. Retry the same request."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Credit note save could not be confirmed.");

                return StatusCode(503, new
                {
                    message =
                        "The credit note save could not be confirmed. Retry the same request."
                });
            }
        }
    }
}