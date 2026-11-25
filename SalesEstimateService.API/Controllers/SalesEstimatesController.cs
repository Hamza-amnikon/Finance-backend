using SparkFinance.Http;
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
        private readonly FinanceCompanyAccess _access;
        private readonly ILogger<SalesEstimatesController> _logger;

        public SalesEstimatesController(
            ISalesEstimateService service,
            ISalesEstimateConversionService conversionService,
            FinanceCompanyAccess access,
            ILogger<SalesEstimatesController> logger)
        {
            _service = service;
            _conversionService = conversionService;
            _access = access;
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
                var scope = await _access.GetAsync(Request, "/sales/estimates", cancellationToken);
                Response.Headers.CacheControl = "no-store";
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                query.AllowedBranches = scope.Branches;
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
                var scope = await _access.GetAsync(Request, "/sales/estimates", cancellationToken);
                Response.Headers.CacheControl = "no-store";
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var estimate = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                return estimate == null || !scope.Allows(estimate.Branch)
                    ? NotFound(new
                    {
                        message = "Estimate not found."
                    })
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
                var scope = await _access.GetAsync(Request, "/sales/estimates", cancellationToken);
                if (!scope.AllowsAction(dto.Branch, scope.CreatableBranches))
                {
                    return StatusCode(scope.Status == 200 ? 403 : scope.Status);
                }

                var estimate = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new
                    {
                        id = estimate.Id
                    },
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
                var scope = await _access.GetAsync(Request, "/sales/estimates", cancellationToken);
                if (!scope.AllowsAction(dto.Branch, scope.CreatableBranches) &&
                    !scope.AllowsAction(dto.Branch, scope.EditableBranches))
                {
                    return StatusCode(scope.Status == 200 ? 403 : scope.Status);
                }

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
                var scope = await _access.GetAsync(Request, "/sales/estimates", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var existing = await _service.GetByIdAsync(id, cancellationToken);
                if (existing == null || !scope.Allows(existing.Branch))
                {
                    return NotFound();
                }

                if (!scope.AllowsAction(existing.Branch, scope.EditableBranches) ||
                    !scope.AllowsAction(dto.Branch, scope.EditableBranches))
                {
                    return StatusCode(403);
                }

                var estimate = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new
                    {
                        message = "Estimate not found."
                    })
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
                var denied = await DenyExistingActionAsync(id, "canIssue", cancellationToken);
                if (denied != null)
                {
                    return denied;
                }

                var estimate = await _service.MarkAsSentAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new
                    {
                        message = "Estimate not found."
                    })
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
                var denied = await DenyExistingActionAsync(id, "canApprove", cancellationToken);
                if (denied != null)
                {
                    return denied;
                }

                var estimate = await _service.AcceptAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new
                    {
                        message = "Estimate not found."
                    })
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
                var denied = await DenyExistingActionAsync(id, "canCancel", cancellationToken);
                if (denied != null)
                {
                    return denied;
                }

                var estimate = await _service.DeclineAsync(
                    id,
                    dto,
                    cancellationToken);

                return estimate == null
                    ? NotFound(new
                    {
                        message = "Estimate not found."
                    })
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
                var denied = await DenyExistingActionAsync(id, "canView", cancellationToken);
                if (denied != null)
                {
                    return denied;
                }

                var existing = await _service.GetByIdAsync(id, cancellationToken);
                var invoiceScope = await _access.GetAsync(Request, "/sales/invoices", cancellationToken);
                if (!invoiceScope.AllowsAction(existing!.Branch, invoiceScope.CreatableBranches))
                {
                    return StatusCode(invoiceScope.Status == 200 ? 403 : invoiceScope.Status);
                }

                var result = await _conversionService.ConvertAsync(id, dto, cancellationToken);

                return result == null
                    ? NotFound(new
                    {
                        message = "Estimate not found."
                    })
                    : Ok(result);
            }, cancellationToken);
        }

        private async Task<IActionResult?> DenyExistingActionAsync(
            int id,
            string action,
            CancellationToken cancellationToken)
        {
            var scope = await _access.GetAsync(Request, "/sales/estimates", cancellationToken);
            if (scope.Status != 200)
            {
                return StatusCode(scope.Status);
            }

            var existing = await _service.GetByIdAsync(id, cancellationToken);
            if (existing == null || !scope.Allows(existing.Branch))
            {
                return NotFound();
            }

            if (action == "canView")
            {
                return null;
            }

            var branches = action switch
            {
                "canIssue" => scope.IssuableBranches,
                "canApprove" => scope.ApprovableBranches,
                "canCancel" => scope.CancellableBranches,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };

            return scope.AllowsAction(existing.Branch, branches) ? null : StatusCode(403);
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
                return Conflict(new
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
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
