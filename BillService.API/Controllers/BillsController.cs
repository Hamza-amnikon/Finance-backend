using System.ComponentModel.DataAnnotations;
using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;
using BillService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BillService.API.Controllers
{
    [ApiController]
    [Route("api/bills")]
    public class BillsController : ControllerBase
    {
        private readonly IBillService _service;
        private readonly BillDbContext _context;
        private readonly IBillConversionService _conversionService;
        private readonly ILogger<BillsController> _logger;

        public BillsController(
            IBillService service,
            BillDbContext context,
            IBillConversionService conversionService,
            ILogger<BillsController> logger)
        {
            _service = service;
            _context = context;
            _conversionService = conversionService;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] BillQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
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
            }, cancellationToken);
        }

        [HttpGet("{id:int}")]
        public Task<IActionResult> GetById(
            [Range(1, int.MaxValue)] int id,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var bill = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                if (bill == null)
                {
                    return NotFound(new
                    {
                        message = "Bill not found."
                    });
                }

                return Ok(bill);
            }, cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreateBillDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var bill = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = bill.Id },
                    bill);
            }, cancellationToken);
        }

        [HttpPost("preview")]
        public Task<IActionResult> Preview(
            [FromBody] BillFieldsDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var bill = await _service.PreviewAsync(
                    dto,
                    cancellationToken);

                return Ok(bill);
            }, cancellationToken);
        }

        [HttpPut("{id:int}")]
        public Task<IActionResult> Update(
            [Range(1, int.MaxValue)] int id,
            [FromBody] UpdateBillDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var bill = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (bill == null)
                {
                    return NotFound(new
                    {
                        message = "Bill not found."
                    });
                }

                return Ok(bill);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/issue")]
        public Task<IActionResult> Issue(
            [Range(1, int.MaxValue)] int id,
            [FromBody] BillActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var bill = await _service.ChangeStatusAsync(
                    id,
                    BillStatus.Issued,
                    dto.RowVersion,
                    cancellationToken);

                if (bill == null)
                {
                    return NotFound(new
                    {
                        message = "Bill not found."
                    });
                }

                return Ok(bill);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/cancel")]
        public Task<IActionResult> Cancel(
            [Range(1, int.MaxValue)] int id,
            [FromBody] BillActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var bill = await _service.ChangeStatusAsync(
                    id,
                    BillStatus.Cancelled,
                    dto.RowVersion,
                    cancellationToken);

                if (bill == null)
                {
                    return NotFound(new
                    {
                        message = "Bill not found."
                    });
                }

                return Ok(bill);
            }, cancellationToken);
        }

        [HttpPost("from-purchase-order/{purchaseOrderId:int}")]
        public Task<IActionResult> ConvertPurchaseOrder(
            [Range(1, int.MaxValue)] int purchaseOrderId,
            [FromBody] ConvertPurchaseOrderDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var bill = await _conversionService.ConvertAsync(
                    purchaseOrderId,
                    dto.RowVersion,
                    cancellationToken);

                if (bill == null)
                {
                    return NotFound(new
                    {
                        message = "Purchase bill not found."
                    });
                }

                return Ok(bill);
            }, cancellationToken);
        }

        [HttpGet("summary")]
        public Task<IActionResult> Summary(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var groups = await _context.Bills
                    .AsNoTracking()
                    .GroupBy(bill => bill.Status)
                    .Select(group => new
                    {
                        Status = group.Key,
                        Count = group.Count()
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new
                {
                    total = groups.Sum(group => group.Count),
                    draft = groups.Where(group => group.Status == BillStatus.Draft)
                        .Sum(group => group.Count),
                    issued = groups.Where(group => group.Status == BillStatus.Issued)
                        .Sum(group => group.Count),
                    cancelled = groups.Where(group => group.Status == BillStatus.Cancelled)
                        .Sum(group => group.Count)
                });
            }, cancellationToken);
        }

        [HttpGet("lookups/vendors")]
        public Task<IActionResult> Vendors(
            CancellationToken cancellationToken,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 50,
            [FromQuery, MaxLength(200)] string? search = null)
        {
            return HandleReadAsync(async () =>
            {
                var query = _context.Vendors
                    .AsNoTracking()
                    .Where(vendor => vendor.IsActive);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var value = search.Trim();

                    query = query.Where(vendor =>
                        vendor.Name.Contains(value) ||
                        vendor.CompanyName.Contains(value));
                }

                var total = await query.CountAsync(cancellationToken);
                var offset = ((long)page - 1) * pageSize;
                var vendors = offset >= total
                    ? new List<PurchaseVendorLookup>()
                    : await query
                        .OrderBy(vendor => vendor.Name)
                        .ThenBy(vendor => vendor.Id)
                        .Skip((int)offset)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                return Ok(new
                {
                    data = vendors,
                    total,
                    page,
                    pageSize
                });
            }, cancellationToken);
        }

        [HttpGet("lookups/currencies")]
        public Task<IActionResult> Currencies(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var currencies = await _context.Currencies
                    .AsNoTracking()
                    .Where(currency => currency.IsActive)
                    .OrderBy(currency => currency.CurrencyCode)
                    .ToListAsync(cancellationToken);

                return Ok(currencies);
            }, cancellationToken);
        }

        [HttpGet("lookups/branches")]
        public Task<IActionResult> Branches(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(branch => branch.IsActive)
                    .OrderBy(branch => branch.Name)
                    .ToListAsync(cancellationToken);

                return Ok(branches);
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
                    message = "This bill changed. Refresh it before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sqlException &&
                      (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return Conflict(new
                {
                    message = "This vendor invoice number or source order is already recorded. Refresh and review the existing bill."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to save bill.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unable to save bill changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Bill database unavailable.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Bill database is temporarily unavailable.");
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
                    message = "This bill changed. Refresh it before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sqlException &&
                      (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return Conflict(new
                {
                    message = "This vendor invoice number or source order is already recorded. Refresh and review the existing bill."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to save bill.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unable to save bill changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Bill database unavailable.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Bill database is temporarily unavailable.");
            }
        }
    }
}
