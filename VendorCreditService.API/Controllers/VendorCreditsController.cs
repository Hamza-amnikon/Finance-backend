using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VendorCreditService.API.DTOs;
using VendorCreditService.API.Data;
using VendorCreditService.API.Models;
using VendorCreditService.API.Services;

namespace VendorCreditService.API.Controllers
{
    [ApiController]
    [Route("api/vendor-credits")]
    public class VendorCreditsController : ControllerBase
    {
        private readonly IVendorCreditService _service;
        private readonly VendorCreditDbContext _context;
        private readonly ILogger<VendorCreditsController> _logger;

        public VendorCreditsController(
            IVendorCreditService service,
            VendorCreditDbContext context,
            ILogger<VendorCreditsController> logger)
        {
            _service = service;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] VendorCreditQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var result = await _service.GetPagedAsync(
                    query,
                    cancellationToken);

                return Ok(new
                {
                    data = result.Data,
                    total = result.Total,
                    page = query.Page,
                    pageSize = query.PageSize
                });
            });
        }

        [HttpGet("{id:int}")]
        public Task<IActionResult> GetById(
            [Range(1, int.MaxValue)] int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var credit = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                if (credit == null)
                {
                    return NotFound(new
                    {
                        message = "Vendor credit not found."
                    });
                }

                return Ok(credit);
            });
        }

        [HttpPost("preview")]
        public Task<IActionResult> Preview(
            [FromBody] VendorCreditFieldsDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var credit = await _service.PreviewAsync(
                    dto,
                    cancellationToken);

                return Ok(credit);
            });
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateVendorCreditDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var credit = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = credit.Id },
                    credit);
            });
        }

        [HttpPut("{id:int}")]
        public Task<IActionResult> Update(
            [Range(1, int.MaxValue)] int id,
            [FromBody] UpdateVendorCreditDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var credit = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (credit == null)
                {
                    return NotFound(new
                    {
                        message = "Vendor credit not found."
                    });
                }

                return Ok(credit);
            });
        }

        [HttpPost("{id:int}/issue")]
        public Task<IActionResult> Issue(
            [Range(1, int.MaxValue)] int id,
            [FromBody] VendorCreditActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var credit = await _service.ChangeStatusAsync(
                    id,
                    VendorCreditStatus.Issued,
                    dto.RowVersion,
                    cancellationToken);

                if (credit == null)
                {
                    return NotFound(new
                    {
                        message = "Vendor credit not found."
                    });
                }

                return Ok(credit);
            });
        }

        [HttpPost("{id:int}/cancel")]
        public Task<IActionResult> Cancel(
            [Range(1, int.MaxValue)] int id,
            [FromBody] VendorCreditActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var credit = await _service.ChangeStatusAsync(
                    id,
                    VendorCreditStatus.Cancelled,
                    dto.RowVersion,
                    cancellationToken);

                if (credit == null)
                {
                    return NotFound(new
                    {
                        message = "Vendor credit not found."
                    });
                }

                return Ok(credit);
            });
        }

        [HttpGet("lookups/vendors")]
        public Task<IActionResult> Vendors(
            CancellationToken cancellationToken,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 50,
            [FromQuery, MaxLength(200)] string? search = null)
        {
            return HandleAsync(async () =>
            {
                var query = _context.Vendors
                    .AsNoTracking()
                    .Where(row => row.IsActive);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var text = search.Trim();

                    query = query.Where(row =>
                        row.Name.Contains(text) ||
                        row.CompanyName.Contains(text));
                }

                var total = await query.CountAsync(cancellationToken);
                var offset = ((long)page - 1) * pageSize;

                var data = offset >= total
                    ? new List<PurchaseVendorLookup>()
                    : await query
                        .OrderBy(row => row.Name)
                        .ThenBy(row => row.Id)
                        .Skip((int)offset)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                return Ok(new { data, total, page, pageSize });
            });
        }

        [HttpGet("lookups/branches")]
        public Task<IActionResult> Branches(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(row => row.IsActive)
                    .OrderBy(row => row.Name)
                    .ToListAsync(cancellationToken);

                return Ok(branches);
            });
        }

        [HttpGet("lookups/currencies")]
        public Task<IActionResult> Currencies(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var currencies = await _context.Currencies
                    .AsNoTracking()
                    .Where(row => row.IsActive)
                    .OrderBy(row => row.CurrencyCode)
                    .ToListAsync(cancellationToken);

                return Ok(currencies);
            });
        }

        [HttpGet("lookups/bills")]
        public Task<IActionResult> Bills(
            [FromQuery] Guid vendorId,
            CancellationToken cancellationToken,
            [FromQuery, MaxLength(20)] string? branch = null,
            [FromQuery, MaxLength(3)] string? currency = null,
            [FromQuery, MaxLength(200)] string? search = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 50)
        {
            return HandleAsync(async () =>
            {
                if (vendorId == Guid.Empty)
                {
                    return BadRequest(new
                    {
                        message = "Select a vendor."
                    });
                }

                var query = _context.Bills
                    .AsNoTracking()
                    .Where(row =>
                        row.VendorId == vendorId &&
                        row.Status == "Issued");

                if (!string.IsNullOrWhiteSpace(branch))
                {
                    var name = branch.Trim();

                    query = query.Where(row => row.Branch == name);
                }

                if (!string.IsNullOrWhiteSpace(currency))
                {
                    var code = currency.Trim().ToUpperInvariant();

                    query = query.Where(row => row.Currency == code);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var text = search.Trim();

                    query = query.Where(row =>
                        (row.BillNumber != null &&
                            row.BillNumber.Contains(text)) ||
                        row.VendorInvoiceNumber.Contains(text));
                }

                var total = await query.CountAsync(cancellationToken);
                var offset = ((long)page - 1) * pageSize;

                var data = offset >= total
                    ? new List<PurchaseBillLookup>()
                    : await query
                        .OrderByDescending(row => row.BillDate)
                        .ThenByDescending(row => row.Id)
                        .Skip((int)offset)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                return Ok(new { data, total, page, pageSize });
            });
        }

        private async Task<IActionResult> HandleAsync(
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
                        "This vendor credit changed. Refresh it before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                _logger.LogWarning(
                    exception,
                    "Duplicate vendor credit record.");

                return Conflict(new
                {
                    message =
                        "A vendor credit with the same unique details already exists. Refresh and review the existing records."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to save vendor credit.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unable to save vendor credit changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Vendor credit database unavailable.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Vendor credit database is temporarily unavailable.");
            }
        }
    }
}