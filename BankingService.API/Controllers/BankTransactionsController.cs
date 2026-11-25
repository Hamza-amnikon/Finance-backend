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
    [Route("api/bank-transactions")]
    public class BankTransactionsController : ControllerBase
    {
        private readonly IBankTransactionService _service;
        private readonly IBankAccountService _accounts;
        private readonly FinanceCompanyAccess _access;
        private readonly ILogger<BankTransactionsController> _logger;

        public BankTransactionsController(
            IBankTransactionService service,
            IBankAccountService accounts,
            FinanceCompanyAccess access,
            ILogger<BankTransactionsController> logger)
        {
            _service = service;
            _accounts = accounts;
            _access = access;
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
                    var scope = await _access.GetAsync(Request, "/banking/transactions", cancellationToken);
                    Response.Headers.CacheControl = "no-store";
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
        public Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/transactions", cancellationToken);
                    Response.Headers.CacheControl = "no-store";
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var result = await _service.GetByIdAsync(
                        id,
                        cancellationToken);

                    if (result == null || !scope.AllowsCompany(result.CompanyId))
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
                    var scope = await _access.GetAsync(Request, "/banking/transactions", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var account = await _accounts.GetByIdAsync(dto.BankAccountId, cancellationToken);
                    if (account == null || !scope.AllowsCompany(account.CompanyId))
                    {
                        return TransactionNotFound();
                    }

                    if (!scope.AllowsCompanyAction(account.CompanyId, scope.CreatableCompanyIds))
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
            [FromBody] UpdateBankTransactionDto dto,
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
                    var denied = await DenyActionAsync(id, "post", cancellationToken);
                    if (denied != null)
                    {
                        return denied;
                    }

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
                        return TransactionNotFound();
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
                var scope = await _access.GetAsync(Request, "/banking/transactions", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                query.AllowedCompanyIds = scope.CompanyIds;
                return Ok(await _accounts.GetPagedAsync(query, cancellationToken));
            }, cancellationToken);
        }

        [HttpGet("lookups/payment-accounts")]
        public Task<IActionResult> PaymentAccounts(
            [FromQuery] BankAccountQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var scope = await _access.GetAsync(Request, "/sales/payments", cancellationToken);
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
                var scope = await _access.GetAsync(Request, "/banking/transactions", cancellationToken);
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
            var scope = await _access.GetAsync(Request, "/banking/transactions", cancellationToken);
            if (scope.Status != 200)
            {
                return StatusCode(scope.Status);
            }

            var transaction = await _service.GetByIdAsync(id, cancellationToken);
            if (transaction == null || !scope.AllowsCompany(transaction.CompanyId))
            {
                return TransactionNotFound();
            }

            var allowedCompanyIds = action switch
            {
                "edit" => scope.EditableCompanyIds,
                "post" => scope.PostableCompanyIds,
                "cancel" => scope.CancellableCompanyIds,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };

            return scope.AllowsCompanyAction(transaction.CompanyId, allowedCompanyIds)
                ? null
                : StatusCode(403);
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
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
