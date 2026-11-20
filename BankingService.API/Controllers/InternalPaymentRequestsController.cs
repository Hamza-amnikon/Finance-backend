using BankingService.API.DTOs;
using BankingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Http;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/internal-payment-requests")]
    public sealed class InternalPaymentRequestsController(
        InternalPaymentRequestAccess access,
        InternalPaymentRequestService service,
        IConfiguration configuration,
        ILogger<InternalPaymentRequestsController> logger) : ControllerBase
    {
        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery] int page = 1,
            [FromQuery] int? companyId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            return HandleAsync(async scope =>
            {
                if (page < 1 || page > 100000)
                {
                    return BadRequest(new { message = "Choose a valid page." });
                }
                return Ok(await service.GetPagedAsync(scope, page, cancellationToken, companyId, status, search));
            }, cancellationToken);
        }

        [HttpGet("setup")]
        public Task<IActionResult> Setup(CancellationToken cancellationToken)
        {
            return HandleAsync(async scope => Ok(await service.GetSetupAsync(
                scope,
                configuration["InternalPaymentRequests:DisplayName"] ?? "Internal Payment Request",
                cancellationToken)), cancellationToken);
        }

        [HttpGet("{id:int:min(1)}")]
        public Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            return HandleAsync(async scope => Ok(await service.GetDetailAsync(id, scope, cancellationToken)), cancellationToken);
        }

        [HttpGet("{id:int:min(1)}/print")]
        public Task<IActionResult> Print(int id, CancellationToken cancellationToken)
        {
            return HandleAsync(async scope => Ok(await service.GetPrintableAsync(
                id,
                scope,
                configuration["InternalPaymentRequests:DisplayName"] ?? "Internal Payment Request",
                cancellationToken)), cancellationToken);
        }

        [HttpGet("{id:int:min(1)}/transfer-candidates")]
        public Task<IActionResult> TransferCandidates(int id, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
        {
            return HandleAsync(async scope =>
            {
                var transferScope = await access.GetAsync(Request, cancellationToken, "/banking/transfers");
                return Ok(await service.GetTransferCandidatesAsync(id, page, scope, transferScope, cancellationToken));
            }, cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/transfers")]
        public Task<IActionResult> LinkTransfer(int id, InternalPaymentRequestTransferDto dto, CancellationToken cancellationToken)
        {
            return HandleAsync(async scope =>
            {
                var transferScope = await access.GetAsync(Request, cancellationToken, "/banking/transfers");
                return Ok(await service.LinkTransferAsync(id, dto, scope, transferScope, cancellationToken));
            }, cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(InternalPaymentRequestDto dto, CancellationToken cancellationToken)
        {
            return HandleAsync(async scope =>
            {
                var result = await service.CreateAsync(dto, scope, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }, cancellationToken);
        }

        [HttpPut("{id:int:min(1)}")]
        public Task<IActionResult> Update(int id, InternalPaymentRequestDto dto, CancellationToken cancellationToken)
        {
            return HandleAsync(async scope => Ok(await service.UpdateAsync(id, dto, scope, cancellationToken)), cancellationToken);
        }

        [HttpPost("{id:int:min(1)}/{operation}")]
        public Task<IActionResult> ChangeStatus(
            int id,
            [FromRoute] string operation,
            InternalPaymentRequestActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async scope => Ok(await service.ChangeStatusAsync(id, operation, dto, scope, cancellationToken)), cancellationToken);
        }

        private async Task<IActionResult> HandleAsync(
            Func<InternalPaymentRequestScope, Task<IActionResult>> operation,
            CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";
            try
            {
                var scope = await access.GetAsync(Request, cancellationToken);
                return await operation(scope);
            }
            catch (InternalPaymentRequestException exception)
            {
                return StatusCode(exception.Status, new { message = exception.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "The request changed. Reload it before trying again." });
            }
            catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                return Conflict(new { message = "The request or transfer link already exists. Refresh before trying again." });
            }
            catch (Exception exception) when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Internal payment request operation failed.");
                return StatusCode(503, new { message = "The request service is unavailable. Check the database migration and retry." });
            }
        }
    }
}
