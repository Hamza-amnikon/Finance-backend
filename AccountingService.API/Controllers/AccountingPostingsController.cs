using SparkFinance.Http;
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

        private readonly ISalesCreditNotePostingService
            _salesCreditNotePostingService;

        private readonly IPurchaseBillPostingService
            _purchaseBillPostingService;

        private readonly IPaymentReceivedPostingService
            _paymentReceivedPostingService;

        private readonly IPaymentMadePostingService
            _paymentMadePostingService;

        private readonly ILogger<AccountingPostingsController>
            _logger;

        public AccountingPostingsController(
            ISalesInvoicePostingService salesInvoicePostingService,
            ISalesCreditNotePostingService salesCreditNotePostingService,
            IPurchaseBillPostingService purchaseBillPostingService,
            IPaymentReceivedPostingService paymentReceivedPostingService,
            IPaymentMadePostingService paymentMadePostingService,
            ILogger<AccountingPostingsController> logger)
        {
            _salesInvoicePostingService =
                salesInvoicePostingService;

            _salesCreditNotePostingService =
                salesCreditNotePostingService;

            _purchaseBillPostingService =
                purchaseBillPostingService;

            _paymentReceivedPostingService =
                paymentReceivedPostingService;

            _paymentMadePostingService =
                paymentMadePostingService;

            _logger = logger;
        }

        [HttpPost("sales-credit-notes")]
        public async Task<IActionResult> PostSalesCreditNote(
            [FromBody] PostSalesCreditNoteDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(
                    await _salesCreditNotePostingService
                        .PostAsync(
                            dto,
                            cancellationToken));
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
                        "This sales credit note was already posted."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Sales credit note accounting request failed.");

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
                    "Sales credit note journal could not be saved.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Sales credit note journal could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Sales credit note accounting request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Sales credit note accounting request failed.");
            }
        }

        [HttpPost("payments-made")]
        public async Task<IActionResult> PostPaymentMade(
            [FromBody] PostPaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(
                    await _paymentMadePostingService
                        .PostAsync(
                            dto,
                            cancellationToken));
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
                        "This supplier payment was already posted."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Supplier payment accounting request failed.");

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
                    "Supplier payment journal could not be saved.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Supplier payment journal could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Supplier payment accounting request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Supplier payment accounting request failed.");
            }
        }

        [HttpPost("payments-received")]
        public async Task<IActionResult> PostPaymentReceived(
            [FromBody] PostPaymentReceivedDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(
                    await _paymentReceivedPostingService
                        .PostAsync(
                            dto,
                            cancellationToken));
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
                        "This received payment was already posted."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Received payment accounting request failed.");

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
                    "Received payment journal could not be saved.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Received payment journal could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Received payment accounting request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Received payment accounting request failed.");
            }
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
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
                when (exception.InnerException is SqlException sqlException &&
                      (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return Conflict(new
                {
                    message = "This purchase bill was already posted."
                });
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
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
