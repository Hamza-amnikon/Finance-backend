using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/bank-transfers")]
    public class BankTransfersController : ControllerBase
    {
        private readonly IBankTransferService _service;
        private readonly ILogger<BankTransfersController> _logger;

        public BankTransfersController(
            IBankTransferService service,
            ILogger<BankTransfersController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] BankTransferQueryDto query,
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
                        return TransferNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateBankTransferDto dto,
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

        [HttpPut("{id:int:min(1)}")]
        public Task<IActionResult> Update(
            int id,
            [FromBody] UpdateBankTransferDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.UpdateAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return TransferNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/confirm-sent")]
        public Task<IActionResult> ConfirmSent(
            int id,
            [FromBody] ConfirmBankTransferSentDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.ConfirmSentAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return TransferNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/confirm-received")]
        public Task<IActionResult> ConfirmReceived(
            int id,
            [FromBody] ConfirmBankTransferReceivedDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.ConfirmReceivedAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return TransferNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/cancel")]
        public Task<IActionResult> Cancel(
            int id,
            [FromBody] BankTransferVersionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.CancelAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return TransferNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        private NotFoundObjectResult TransferNotFound()
        {
            return NotFound(new
            {
                message = "Bank transfer not found."
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
                    "A bank transfer was changed by another request.");

                return Conflict(new
                {
                    message =
                        "This transfer has changed. Refresh it before trying again."
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
                    "A duplicate bank transfer operation was attempted.");

                return Conflict(new
                {
                    message =
                        "This operation conflicts with an existing record. Refresh the transfer before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "A bank transfer operation could not be completed.");

                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "A database error occurred during a bank transfer request.");

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "The database request could not be completed. Refresh the transfer to check its status before retrying."
                    });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "A bank transfer could not be saved.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "The transfer could not be saved. Refresh it to check its status before retrying."
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "An unexpected bank transfer error occurred.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "The transfer request could not be completed. Refresh it to check its status before retrying."
                    });
            }
        }
    }
}