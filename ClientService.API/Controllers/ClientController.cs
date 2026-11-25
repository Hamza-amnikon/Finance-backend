using SparkFinance.Http;
using SparkFinance.Auditing;
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

        private readonly FinanceCompanyAccess _access;

        public ClientController(IClientService service, FinanceCompanyAccess access)
        {
            _service = service;
            _access = access;
        }

        [HttpGet]
        public Task<IActionResult> GetAll(
            [FromQuery] ClientQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var scope = await _access.GetAsync(Request, "/sales/clients", cancellationToken);
                Response.Headers.CacheControl = "no-store";

                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                query.AllowedBranches = scope.Branches;
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
                var scope = await _access.GetAsync(Request, "/sales/clients", cancellationToken);
                Response.Headers.CacheControl = "no-store";

                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var client = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                if (client == null || !scope.Allows(client.Branch))
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
                var scope = await _access.GetAsync(Request, "/sales/clients", cancellationToken);
                if (!scope.AllowsAction(dto.Branch.Trim(), scope.CreatableBranches))
                {
                    return StatusCode(scope.Status == 200 ? 403 : scope.Status);
                }

                SetVerifiedAuditActor(scope);

                var client = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new
                    {
                        id = client.Id
                    },
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
                var scope = await _access.GetAsync(Request, "/sales/clients", cancellationToken);
                if (!scope.AllowsAction(dto.Branch.Trim(), scope.EditableBranches))
                {
                    return StatusCode(scope.Status == 200 ? 403 : scope.Status);
                }

                var existing = await _service.GetByIdAsync(id, cancellationToken);
                if (existing == null || !scope.Allows(existing.Branch))
                {
                    return NotFound();
                }

                if (!scope.AllowsAction(existing.Branch, scope.EditableBranches))
                {
                    return StatusCode(403);
                }

                SetVerifiedAuditActor(scope);

                var client = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (client == null || !scope.Allows(client.Branch))
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
                var scope = await _access.GetAsync(Request, "/sales/clients", cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var existing = await _service.GetByIdAsync(id, cancellationToken);
                if (existing == null || !scope.Allows(existing.Branch))
                {
                    return NotFound();
                }

                if (!scope.AllowsAction(existing.Branch, scope.DeletableBranches))
                {
                    return StatusCode(403);
                }

                SetVerifiedAuditActor(scope);

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

        private void SetVerifiedAuditActor(FinanceCompanyScope scope)
        {
            if (Guid.TryParse(scope.ActorId, out var actorId) && actorId != Guid.Empty)
            {
                HttpContext.Items[FinanceAudit.VerifiedActorItem] = actorId.ToString();
            }
        }

        private async Task<IActionResult> HandleReadAsync(
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
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
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
