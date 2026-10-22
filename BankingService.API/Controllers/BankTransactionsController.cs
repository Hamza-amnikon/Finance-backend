using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/bank-transactions")]
    public class BankTransactionsController : ControllerBase
    {
        private readonly IBankTransactionService _service;
        private readonly ILogger<BankTransactionsController> _logger;

        public BankTransactionsController(
            IBankTransactionService service,
            ILogger<BankTransactionsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetAll(
            [FromQuery] BankTransactionQueryDto query,
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
                        return TransactionNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateBankTransactionDto dto,
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
            [FromBody] UpdateBankTransactionDto dto,
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
                        return TransactionNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/post")]
        public Task<IActionResult> Post(
            int id,
            [FromBody] BankTransactionVersionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result = await _service.PostAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return TransactionNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/cancel")]
        public Task<IActionResult> Cancel(
            int id,
            [FromBody] BankTransactionVersionDto dto,
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
                        return TransactionNotFound();
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        private IActionResult TransactionNotFound()
        {
            return NotFound(new
            {
                message = "Bank transaction not found.",
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
                    message = exception.Message,
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "This transaction has changed. Reload it before saving.",
                });
            }
            catch (InvalidOperationException exception)
                when (
                    exception.Message == BankTransactionHelper.LockedMessage ||
                    exception.Message == BankTransactionService.RequestConflictMessage)
            {
                return Conflict(new
                {
                    message = exception.Message,
                });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException
                {
                    Number: 2601 or 2627
                })
            {
                _logger.LogWarning(
                    exception,
                    "A duplicate bank transaction was rejected.");

                return Conflict(new
                {
                    message =
                        "A matching transaction already exists. Refresh before trying again.",
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Banking database request failed.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Banking database is unavailable.");
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Bank transaction could not be saved.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Bank transaction could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Bank transaction request failed.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Bank transaction request failed.");
            }
        }
    }
}