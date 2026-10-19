using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.DTOs;
using SalesEstimateService.API.Services;

namespace SalesEstimateService.API.Controllers
{
    [ApiController]
    [Route("api/sales-estimates")]
    public class SalesEstimatesController : ControllerBase
    {
        private readonly ISalesEstimateService _service;
        private readonly ISalesEstimateConversionService _conversionService;
        private readonly ILogger<SalesEstimatesController> _logger;

        public SalesEstimatesController(
            ISalesEstimateService service,
            ISalesEstimateConversionService conversionService,
            ILogger<SalesEstimatesController> logger)
        {
            _service = service;
            _conversionService = conversionService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(SalesEstimateListResponseDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetAll(
            [FromQuery] SalesEstimateQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var result = await _service.GetAllAsync(
                    query,
                    cancellationToken);

                return Ok(new SalesEstimateListResponseDto
                {
                    Data = result.Data,
                    Total = result.Total,
                    Page = query.Page,
                    PageSize = query.PageSize
                });
            }, cancellationToken);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetById(
            [Range(1, int.MaxValue)] int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new { message = "Estimate not found." })
                    : Ok(estimate);
            }, cancellationToken);
        }

        [HttpPost]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status201Created)]
        public Task<IActionResult> Create(
            [FromBody] CreateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = estimate.Id },
                    estimate);
            }, cancellationToken);
        }

        [HttpPost("preview")]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status200OK)]
        public Task<IActionResult> Preview(
            [FromBody] CreateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.PreviewAsync(
                    dto,
                    cancellationToken);

                return Ok(estimate);
            }, cancellationToken);
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> Update(
            [Range(1, int.MaxValue)] int id,
            [FromBody] UpdateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new { message = "Estimate not found." })
                    : Ok(estimate);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/send")]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> MarkAsSent(
            [Range(1, int.MaxValue)] int id,
            [FromBody] SalesEstimateActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.MarkAsSentAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new { message = "Estimate not found." })
                    : Ok(estimate);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/accept")]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> Accept(
            [Range(1, int.MaxValue)] int id,
            [FromBody] SalesEstimateActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.AcceptAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new { message = "Estimate not found." })
                    : Ok(estimate);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/decline")]
        [ProducesResponseType(typeof(SalesEstimateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> Decline(
            [Range(1, int.MaxValue)] int id,
            [FromBody] SalesEstimateActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var estimate = await _service.DeclineAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new { message = "Estimate not found." })
                    : Ok(estimate);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/convert-to-invoice")]
        [ProducesResponseType(typeof(SalesEstimateConversionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> ConvertToInvoice(
            [Range(1, int.MaxValue)] int id,
            [FromBody] ConvertSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var result = await _conversionService.ConvertAsync(id, dto, cancellationToken);

                return result == null
                    ? NotFound(new { message = "Estimate not found." })
                    : Ok(result);
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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "Estimate changed since you opened it. Reload before saving."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Estimate save failed.");

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Saving the estimate failed. Retry the same request to recover any completed conversion."
                });
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
                return BadRequest(new
                {
                    message = "Estimate amounts exceed the supported range."
                });
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Estimate client or currency lookup failed.");

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Client or currency service is unavailable. Try again later."
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new
                {
                    message = "Client or currency service took too long to respond. Try again later."
                });
            }
            catch (SqlException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Estimate database operation failed.");

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "The estimate database is unavailable. Try again later."
                });
            }
        }
    }
}
