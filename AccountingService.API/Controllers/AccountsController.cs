using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _service;

        private readonly IChartOfAccountsSetupService
            _setupService;

        private readonly ILogger<AccountsController>
            _logger;

        public AccountsController(
            IAccountService service,
            IChartOfAccountsSetupService setupService,
            ILogger<AccountsController> logger)
        {
            _service = service;
            _setupService = setupService;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetAll(
            [FromQuery] AccountQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.GetPagedAsync(
                            query,
                            cancellationToken);

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpGet("tree")]
        public Task<IActionResult> GetTree(
            [FromQuery]
            [Range(1, int.MaxValue)]
            int companyId,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.GetTreeAsync(
                            companyId,
                            cancellationToken);

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
                    var companies =
                        await _service
                            .GetCompaniesAsync(
                                cancellationToken);

                    var result =
                        companies.Select(
                            company => new
                            {
                                company.Id,
                                company.Name,
                                company.Country,
                                company.Currency
                            });

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpGet("{id:int:min(1)}")]
        public Task<IActionResult> GetById(
            int id,
            [FromQuery]
            [Range(1, int.MaxValue)]
            int companyId,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.GetByIdAsync(
                            id,
                            companyId,
                            cancellationToken);

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            message =
                                "Account not found."
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("setup")]
        public Task<IActionResult> Setup(
            [FromBody] SetupChartOfAccountsDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var accounts =
                        await _setupService.SetupAsync(
                            dto.CompanyId,
                            cancellationToken);

                    return Ok(new
                    {
                        message =
                            "The standard Chart of Accounts " +
                            "was set up successfully.",

                        total =
                            accounts.Count,

                        data =
                            accounts
                    });
                },
                cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateAccountDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.CreateAsync(
                            dto,
                            cancellationToken);

                    return CreatedAtAction(
                        nameof(GetById),
                        new
                        {
                            id = result.Id,
                            companyId =
                                result.CompanyId
                        },
                        result);
                },
                cancellationToken);
        }

        [HttpPut("{id:int:min(1)}")]
        public Task<IActionResult> Update(
            int id,
            [FromQuery]
            [Range(1, int.MaxValue)]
            int companyId,
            [FromBody] UpdateAccountDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.UpdateAsync(
                            id,
                            companyId,
                            dto,
                            cancellationToken);

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            message =
                                "Account not found."
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "This account has changed. " +
                        "Reload it before saving."
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
                        "This account code or system key " +
                        "already exists for the company."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Accounting database request failed.");

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
                    "Account could not be saved.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Account could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Account request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Account request failed.");
            }
        }
    }
}