using ClientService.API.DTOs;
using ClientService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClientService.API.Controllers
{
    [ApiController]
    [Route("api/clients")]
    [Route("api/parties/Customer")]
    public class ClientController : ControllerBase
    {
        private readonly IClientService _service;

        public ClientController(IClientService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> GetAll(
            [FromQuery] ClientQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var result = await _service.GetAllAsync(
                    query,
                    cancellationToken);

                return Ok(new
                {
                    data = result.Data,
                    total = result.Total,
                    page = query.Page,
                    pageSize = query.PageSize
                });
            }, cancellationToken);
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var client = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                if (client == null)
                {
                    return NotFound(new
                    {
                        message = "Client not found."
                    });
                }

                return Ok(client);
            }, cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateClientDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var client = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = client.Id },
                    client);
            }, cancellationToken);
        }

        [HttpPut("{id:guid}")]
        public Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateClientDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var client = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (client == null)
                {
                    return NotFound(new
                    {
                        message = "Client not found."
                    });
                }

                return Ok(client);
            }, cancellationToken);
        }

        [HttpDelete("{id:guid}")]
        public Task<IActionResult> Archive(
            Guid id,
            [FromBody] ArchiveClientDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var archived = await _service.ArchiveAsync(
                    id,
                    dto.RowVersion,
                    cancellationToken);

                if (!archived)
                {
                    return NotFound(new
                    {
                        message = "Client not found."
                    });
                }

                return NoContent();
            }, cancellationToken);
        }

        private async Task<IActionResult> HandleReadAsync(
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
            catch (SqlException exception)
                when (cancellationToken.IsCancellationRequested &&
                      exception.Number == 0)
            {
                return StatusCode(499);
            }
        }

        private async Task<IActionResult> HandleSaveAsync(
            Func<Task<IActionResult>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                return await action();
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "Unable to validate the managing branch. " +
                            "Check that the branch API is running."
                    });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    new
                    {
                        message = "Branch validation timed out. Try again later."
                    });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "This client was changed by another user. " +
                        "Refresh the record and try again."
                });
            }
        }
    }
}