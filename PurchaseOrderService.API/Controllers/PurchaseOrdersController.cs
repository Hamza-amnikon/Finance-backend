using System.ComponentModel.DataAnnotations;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Models;
using PurchaseOrderService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PurchaseOrderService.API.Controllers
{
    [ApiController]
    [Route("api/purchase-orders")]
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly IPurchaseOrderService _service;
        private readonly PurchaseOrderDbContext _context;
        private readonly ILogger<PurchaseOrdersController> _logger;

        public PurchaseOrdersController(
            IPurchaseOrderService service,
            PurchaseOrderDbContext context,
            ILogger<PurchaseOrdersController> logger)
        {
            _service = service;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] PurchaseOrderQueryDto query,
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
                var order = await _service.GetByIdAsync(
                    id,
                    cancellationToken);

                if (order == null)
                {
                    return NotFound(new
                    {
                        message = "Purchase order not found."
                    });
                }

                return Ok(order);
            }, cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreatePurchaseOrderDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var order = await _service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = order.Id },
                    order);
            }, cancellationToken);
        }

        [HttpPost("preview")]
        public Task<IActionResult> Preview(
            [FromBody] PurchaseOrderFieldsDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var order = await _service.PreviewAsync(
                    dto,
                    cancellationToken);

                return Ok(order);
            }, cancellationToken);
        }

        [HttpPut("{id:int}")]
        public Task<IActionResult> Update(
            [Range(1, int.MaxValue)] int id,
            [FromBody] UpdatePurchaseOrderDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var order = await _service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (order == null)
                {
                    return NotFound(new
                    {
                        message = "Purchase order not found."
                    });
                }

                return Ok(order);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/issue")]
        public Task<IActionResult> Issue(
            [Range(1, int.MaxValue)] int id,
            [FromBody] PurchaseOrderActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var order = await _service.ChangeStatusAsync(
                    id,
                    PurchaseOrderStatus.Issued,
                    dto.RowVersion,
                    cancellationToken);

                if (order == null)
                {
                    return NotFound(new
                    {
                        message = "Purchase order not found."
                    });
                }

                return Ok(order);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/cancel")]
        public Task<IActionResult> Cancel(
            [Range(1, int.MaxValue)] int id,
            [FromBody] PurchaseOrderActionDto dto,
            CancellationToken cancellationToken)
        {
            return HandleSaveAsync(async () =>
            {
                var order = await _service.ChangeStatusAsync(
                    id,
                    PurchaseOrderStatus.Cancelled,
                    dto.RowVersion,
                    cancellationToken);

                if (order == null)
                {
                    return NotFound(new
                    {
                        message = "Purchase order not found."
                    });
                }

                return Ok(order);
            }, cancellationToken);
        }

        [HttpGet("summary")]
        public Task<IActionResult> Summary(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var groups = await _context.PurchaseOrders
                    .AsNoTracking()
                    .GroupBy(order => order.Status)
                    .Select(group => new
                    {
                        Status = group.Key,
                        Count = group.Count()
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new
                {
                    total = groups.Sum(group => group.Count),

                    draft = groups
                        .Where(group =>
                            group.Status == PurchaseOrderStatus.Draft)
                        .Sum(group => group.Count),

                    issued = groups
                        .Where(group =>
                            group.Status == PurchaseOrderStatus.Issued)
                        .Sum(group => group.Count),

                    cancelled = groups
                        .Where(group =>
                            group.Status == PurchaseOrderStatus.Cancelled)
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

                var total = await query.CountAsync(
                    cancellationToken);

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

        [HttpGet("lookups/payment-modes")]
        public Task<IActionResult> PaymentModes(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var paymentModes = await _context.PaymentModes
                    .AsNoTracking()
                    .Where(mode => mode.IsActive)
                    .OrderBy(mode => mode.Name)
                    .Select(mode => new
                    {
                        mode.Id,
                        mode.Name
                    })
                    .ToListAsync(cancellationToken);

                return Ok(paymentModes);
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
                    message =
                        "This purchase order changed. Refresh it before trying again."
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
            {
                _logger.LogError(
                    exception,
                    "Unable to save purchase order.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unable to save purchase order changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Purchase order database unavailable.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Purchase order database is temporarily unavailable.");
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
                    message =
                        "This purchase order changed. Refresh it before trying again."
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
            {
                _logger.LogError(
                    exception,
                    "Unable to save purchase order.");

                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unable to save purchase order changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Purchase order database unavailable.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Purchase order database is temporarily unavailable.");
            }
        }
    }
}