using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/bank-reconciliations")]
    public class BankReconciliationsController : ControllerBase
    {
        private readonly IBankReconciliationService _service;
        private readonly ILogger<BankReconciliationsController> _logger;

        public BankReconciliationsController(
            IBankReconciliationService service,
            ILogger<BankReconciliationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] BankReconciliationQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.GetPagedAsync(
                        query,
                        cancellationToken);

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpGet("{id:int:min(1)}")]
        public Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.GetByIdAsync(
                        id,
                        cancellationToken);

                    if (result == null)
                    {
                        return ReconciliationNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateBankReconciliationDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.CreateAsync(
                        dto,
                        cancellationToken);

                    return CreatedAtAction(
                        nameof(GetById),
                        new { id = result.Id },
                        result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/lines/{lineId:int:min(1)}/match")]
        public Task<IActionResult> Match(
            int id,
            int lineId,
            [FromBody] MatchBankStatementLineDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.MatchAsync(
                        id,
                        lineId,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return ReconciliationNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/complete")]
        public Task<IActionResult> Complete(
            int id,
            [FromBody] BankReconciliationVersionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.CompleteAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return ReconciliationNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        private NotFoundObjectResult ReconciliationNotFound()
        {
            return NotFound(new
            {
                message = "Bank reconciliation not found."
            });
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
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "A reconciliation was changed by another request.");

                return Conflict(new
                {
                    message =
                        "This reconciliation has changed. Refresh it before trying again."
                });
            }
            catch (DbUpdateException exception)
                when (
                    exception.InnerException is SqlException sqlException &&
                    (
                        sqlException.Number == 2601 ||
                        sqlException.Number == 2627
                    ))
            {
                _logger.LogWarning(
                    exception,
                    "A duplicate reconciliation or transaction match was attempted.");

                return Conflict(new
                {
                    message =
                        "The statement or transaction match already exists. Refresh before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "A reconciliation operation could not be completed.");

                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (SqlException exception)
                when (exception.Number == 1205)
            {
                _logger.LogWarning(
                    exception,
                    "A reconciliation database transaction was interrupted by a deadlock.");

                return Conflict(new
                {
                    message =
                        "Another database operation interrupted this request. Refresh and try again."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "A database error occurred during reconciliation.");

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "The database request could not be completed. Refresh to check the reconciliation before retrying."
                    });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Reconciliation changes could not be saved.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "The reconciliation could not be saved. Refresh to check its status before retrying."
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "An unexpected reconciliation error occurred.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "The reconciliation request could not be completed. Refresh before retrying."
                    });
            }
        }
    }
}