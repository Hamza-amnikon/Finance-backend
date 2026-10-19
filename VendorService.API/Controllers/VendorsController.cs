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

        public VendorsController(
            IVendorService vendorService,
            ILogger<VendorsController> logger)
        {
            _vendorService = vendorService;
            _logger = logger;
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
                var (vendors, total) =
                    await _vendorService.GetPagedAsync(
                        page,
                        pageSize,
                        search,
                        isActive,
                        cancellationToken);

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
                var vendor = await _vendorService.GetByIdAsync(
                    id,
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
                    new { id = vendor.Id },
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

        private async Task<IActionResult> ExecuteAsync(
            Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
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