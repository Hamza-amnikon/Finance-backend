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
    [Route("api/bank-accounts")]
    public class BankAccountsController : ControllerBase
    {
        private readonly IBankAccountService _service;
        private readonly FinanceCompanyAccess _access;
        private readonly ILogger<BankAccountsController> _logger;

        public BankAccountsController(
            IBankAccountService service,
            FinanceCompanyAccess access,
            ILogger<BankAccountsController> logger)
        {
            _service = service;
            _access = access;
            _logger = logger;
        }

        [HttpGet]
        [FinanceBranchRead("/banking/accounts", companyScoped: true)]
        public Task<IActionResult> GetAll(
            [FromQuery] BankAccountQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
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
        [FinanceBranchRead("/banking/accounts", detail: true, companyScoped: true)]
        public Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
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
                        return NotFound(new
                        {
                            message = "Bank account not found.",
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateBankAccountDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
                    if (!scope.AllowsCompanyAction(dto.CompanyId, scope.CreatableCompanyIds))
                    {
                        return StatusCode(scope.Status == 200 ? 403 : scope.Status);
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
            [FromBody] UpdateBankAccountDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var denied = await DenyAccountEditAsync(id, cancellationToken);
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
                        return NotFound(new
                        {
                            message = "Bank account not found.",
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/opening-balance")]
        public Task<IActionResult> ConfigureOpeningBalance(
            int id,
            [FromBody] ConfigureOpeningBalanceDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var denied = await DenyAccountEditAsync(id, cancellationToken);
                    if (denied != null)
                    {
                        return denied;
                    }

                    var result = await _service.ConfigureOpeningBalanceAsync(
                        id,
                        dto,
                        cancellationToken);

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            message = "Bank account not found.",
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpGet("{id:int:min(1)}/balance")]
        [FinanceBranchRead("/banking/accounts", detail: true, companyScoped: true)]
        public Task<IActionResult> GetBalance(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var account = await _service.GetByIdAsync(id, cancellationToken);
                    if (account == null || !scope.AllowsCompany(account.CompanyId))
                    {
                        return NotFound();
                    }

                    var result = await _service.GetBalanceAsync(
                        id,
                        cancellationToken);

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            message = "Bank account not found.",
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpGet("lookups/companies")]
        public Task<IActionResult> GetCompanies(
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var result = await _service.GetCompaniesAsync(
                        cancellationToken);

                    return Ok(result.Where(company => scope.AllowsCompany(company.Id)));
                },
                cancellationToken);
        }

        [HttpGet("lookups/currencies")]
        public Task<IActionResult> GetCurrencies(
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
                    if (scope.Status != 200)
                    {
                        return StatusCode(scope.Status);
                    }

                    var result = await _service.GetCurrenciesAsync(
                        cancellationToken);

                    return Ok(result);
                },
                cancellationToken);
        }

        private async Task<IActionResult?> DenyAccountEditAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var scope = await _access.GetAsync(Request, "/banking/accounts", cancellationToken);
            if (scope.Status != 200)
            {
                return StatusCode(scope.Status);
            }

            var account = await _service.GetByIdAsync(id, cancellationToken);
            if (account == null || !scope.AllowsCompany(account.CompanyId))
            {
                return NotFound();
            }

            return scope.AllowsCompanyAction(account.CompanyId, scope.EditableCompanyIds)
                ? null
                : StatusCode(403);
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
                        "This bank account has changed. Reload it before saving.",
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
                    "Bank account could not be saved.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Bank account could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Bank account request failed.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Bank account request failed.");
            }
        }
    }
}
