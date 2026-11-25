using SparkFinance.Http;
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
        private readonly IBankAccountService _accounts;
        private readonly FinanceCompanyAccess _access;
        private readonly ILogger<BankTransfersController> _logger;

        public BankTransfersController(
            IBankTransferService service,
            IBankAccountService accounts,
            FinanceCompanyAccess access,
            ILogger<BankTransfersController> logger)
        {
            _service = service;
            _accounts = accounts;
            _access = access;
            _logger = logger;
        }

        [HttpGet]
        [FinanceBranchRead("/banking/transfers", companyScoped: true)]
        public Task<IActionResult> GetPaged(
            [FromQuery] BankTransferQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/transfers", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    query.AllowedCompanyIds = scope.CompanyIds;
                    var result = await _service.GetPagedAsync(
                        query,
                        cancellationToken);

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpGet("{id:int:min(1)}")]
        [FinanceBranchRead("/banking/transfers", detail: true, companyScoped: true)]
        public Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/transfers", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var result = await _service.GetByIdAsync(
                        id,
                        cancellationToken);

                    if (result == null || !(scope.AllowsCompany(result.SourceCompanyId) || scope.AllowsCompany(result.DestinationCompanyId)))
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
                    var scope = await _access.GetAsync(Request, "/banking/transfers", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var source = await _accounts.GetByIdAsync(dto.SourceBankAccountId, cancellationToken);
                    var destination = await _accounts.GetByIdAsync(dto.DestinationBankAccountId, cancellationToken);
                    if (source == null || destination == null ||
                        !scope.AllowsCompanyAction(source.CompanyId, scope.CreatableCompanyIds) ||
                        !scope.AllowsCompany(destination.CompanyId))
                    {
                        return StatusCode(403);
                    }

                    var result = await _service.CreateAsync(
                        dto,
                        cancellationToken);

                    return CreatedAtAction(
                        nameof(GetById),
                        new
                        {
                            id = result.Id
                        },
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
                    var denied = await DenyActionAsync(id, "edit", cancellationToken);
                    if (denied != null)
                    {
                        return denied;
                    }

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
                    var denied = await DenyActionAsync(id, "send", cancellationToken);
                    if (denied != null)
                    {
                        return denied;
                    }

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
                    var denied = await DenyActionAsync(id, "receive", cancellationToken);
                    if (denied != null)
                    {
                        return denied;
                    }

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
                    var denied = await DenyActionAsync(id, "cancel", cancellationToken);
                    if (denied != null)
                    {
                        return denied;
                    }

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

        [HttpGet("lookups/accounts")]
        public Task<IActionResult> Accounts(
            [FromQuery] BankAccountQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var scope = await _access.GetAsync(Request, "/banking/transfers", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                query.AllowedCompanyIds = scope.CompanyIds;
                return Ok(await _accounts.GetPagedAsync(query, cancellationToken));
            }, cancellationToken);
        }

        [HttpGet("lookups/companies")]
        public Task<IActionResult> Companies(CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var scope = await _access.GetAsync(Request, "/banking/transfers", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var companies = await _accounts.GetCompaniesAsync(cancellationToken);
                return Ok(companies.Where(company => scope.AllowsCompany(company.Id)));
            }, cancellationToken);
        }

        private async Task<IActionResult?> DenyActionAsync(
            int id,
            string action,
            CancellationToken cancellationToken)
        {
            var scope = await _access.GetAsync(Request, "/banking/transfers", cancellationToken);
            if (scope.Status != 200)
            {
                return StatusCode(scope.Status);
            }

            var record = await _service.GetByIdAsync(id, cancellationToken);
            if (record == null || !(scope.AllowsCompany(record.SourceCompanyId) ||
                scope.AllowsCompany(record.DestinationCompanyId)))
            {
                return TransferNotFound();
            }

            var companyId = action == "receive" ? record.DestinationCompanyId : record.SourceCompanyId;
            var companyIds = action switch
            {
                "edit" => scope.EditableCompanyIds,
                "cancel" => scope.CancellableCompanyIds,
                _ => scope.PostableCompanyIds
            };
            return scope.AllowsCompanyAction(companyId, companyIds) ? null : StatusCode(403);
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
