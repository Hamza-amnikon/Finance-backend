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
    [Route("api/journal-entries")]
    public class JournalEntriesController
        : ControllerBase
    {
        private readonly IJournalEntryService
            _service;

        private readonly ILogger<JournalEntriesController>
            _logger;

        public JournalEntriesController(
            IJournalEntryService service,
            ILogger<JournalEntriesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetAll(
            [FromQuery] JournalEntryQueryDto query,
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
                                "Journal entry not found."
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateJournalEntryDto dto,
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
            [FromBody] UpdateJournalEntryDto dto,
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
                                "Journal entry not found."
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/post")]
        public Task<IActionResult> Post(
            int id,
            [FromQuery]
            [Range(1, int.MaxValue)]
            int companyId,
            [FromBody] JournalEntryActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.PostAsync(
                            id,
                            companyId,
                            dto,
                            cancellationToken);

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            message =
                                "Journal entry not found."
                        });
                    }

                    return Ok(result);
                },
                cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/reverse")]
        public Task<IActionResult> Reverse(
            int id,
            [FromQuery]
            [Range(1, int.MaxValue)]
            int companyId,
            [FromBody] JournalEntryActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var result =
                        await _service.ReverseAsync(
                            id,
                            companyId,
                            dto,
                            cancellationToken);

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            message =
                                "Journal entry not found."
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
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message =
                        exception.Message
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "This journal entry has changed. " +
                        "Reload it before continuing."
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
                        "This journal entry already exists " +
                        "or the request was already processed."
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Journal database request failed.");

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
                    "Journal entry could not be saved.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Journal entry could not be saved.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Journal entry request failed.");

                return Problem(
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError,
                    title:
                        "Journal entry request failed.");
            }
        }
    }
}