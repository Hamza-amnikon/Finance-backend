using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VendorService.API.DTOs;
using VendorService.API.Services;

namespace VendorService.API.Controllers
{
    [ApiController]
    [Route("api/vendors")]
    public class VendorsController : ControllerBase
    {
        private readonly IVendorService _vendorService;
        private readonly ILogger<VendorsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly FinanceCompanyAccess _access;

        public VendorsController(
            IVendorService vendorService,
            ILogger<VendorsController> logger,
            IConfiguration configuration,
            FinanceCompanyAccess access)
        {
            _vendorService = vendorService;
            _logger = logger;
            _configuration = configuration;
            _access = access;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            CancellationToken cancellationToken,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            return ExecuteAsync(async () =>
            {
                var scope = await GetReadScopeAsync(cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var (vendors, total) =
                    await _vendorService.GetPagedAsync(
                        page,
                        pageSize,
                        search,
                        isActive,
                        cancellationToken,
                        scope.Branches);

                return Ok(new
                {
                    data = vendors,
                    total,
                    page,
                    pageSize
                });
            });
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var scope = await GetReadScopeAsync(cancellationToken);
                if (scope.Status != 200)
                {
                    return StatusCode(scope.Status);
                }

                var vendor = await _vendorService.GetByIdAsync(
                    id,
                    cancellationToken);

                if (vendor == null || !scope.Allows(vendor.Branch))
                {
                    return NotFound(new
                    {
                        message = "Vendor not found."
                    });
                }

                return Ok(vendor);
            });
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateVendorDto dto,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var vendor = await _vendorService.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new
                    {
                        id = vendor.Id
                    },
                    vendor);
            });
        }

        [HttpPut("{id:guid}")]
        public Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateVendorDto dto,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var vendor = await _vendorService.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (vendor == null)
                {
                    return NotFound(new
                    {
                        message = "Vendor not found."
                    });
                }

                return Ok(vendor);
            });
        }

        [HttpPost("{id:guid}/archive")]
        public Task<IActionResult> Archive(
            Guid id,
            [FromBody] ArchiveVendorDto dto,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var archived = await _vendorService.ArchiveAsync(
                    id,
                    dto.RowVersion,
                    cancellationToken);

                if (!archived)
                {
                    return NotFound(new
                    {
                        message = "Vendor not found."
                    });
                }

                return NoContent();
            });
        }

        private Task<FinanceCompanyScope> GetReadScopeAsync(CancellationToken cancellationToken)
        {
            if (_configuration["Database:Provider"] != "MySql")
            {
                return Task.FromResult(new FinanceCompanyScope(200, null));
            }

            Response.Headers.CacheControl = "no-store";
            return _access.GetAsync(Request, "/purchases/vendors", cancellationToken);
        }

        private async Task<IActionResult> ExecuteAsync(
            Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
                when (HttpContext.RequestAborted.IsCancellationRequested)
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
            catch (ArgumentException exception)
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
                        "This vendor has changed or was removed. " +
                        "Refresh the vendor and try again."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to save vendor changes.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unable to save vendor changes.");
            }
            catch (Exception cancellationException)
                when (RequestCancellation.IsCancellation(
                    cancellationException,
                    HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to access the vendor database.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Vendor database is temporarily unavailable.");
            }
        }
    }
}
