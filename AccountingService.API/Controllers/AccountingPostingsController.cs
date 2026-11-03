using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/accounting-postings")]
    public class AccountingPostingsController
        : ControllerBase
    {
        private readonly ISalesInvoicePostingService
            _salesInvoicePostingService;

        private readonly IPurchaseBillPostingService
            _purchaseBillPostingService;

        private readonly ILogger<AccountingPostingsController>
            _logger;

        public AccountingPostingsController(
            ISalesInvoicePostingService salesInvoicePostingService,
            IPurchaseBillPostingService purchaseBillPostingService,
            ILogger<AccountingPostingsController> logger)
        {
            _salesInvoicePostingService =
                salesInvoicePostingService;

            _purchaseBillPostingService =
                purchaseBillPostingService;

            _logger = logger;
        }

        [HttpPost("purchase-bills")]
        public async Task<IActionResult> PostPurchaseBill(
            [FromBody] PostPurchaseBillDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _purchaseBillPostingService.PostAsync(
                    dto,
                    cancellationToken));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sqlException &&
                      (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return Conflict(new { message = "This purchase bill was already posted." });
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Purchase bill accounting request failed.");
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Accounting database is unavailable.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Purchase bill accounting request failed.");
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Purchase bill accounting request failed.");
            }
        }

        [HttpPost("sales-invoices")]
        public async Task<IActionResult> PostSalesInvoice(
            [FromBody] PostSalesInvoiceDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var result =
                    await _salesInvoicePostingService
                        .PostAsync(
                            dto,
                            cancellationToken);

                return Ok(result);
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
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
            catch (DbUpdateException exception)
                when (
                    exception.InnerException
                        is SqlException sqlException &&
                    (
                        sqlException.Number == 2601 ||
                        sqlException.Number == 2627
                    ))
            {
                return Conflict(new
                {
                    message =
                        "This sales invoice was already posted."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Sales invoice accounting request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status503ServiceUnavailable,
                    title:
                        "Accounting database is unavailable.");
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Sales invoice journal could not be saved.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Sales invoice journal could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Sales invoice accounting request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Sales invoice accounting request failed.");
            }
        }
    }
}
