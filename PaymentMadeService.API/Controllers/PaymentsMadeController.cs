using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentMadeService.API.Data;
using PaymentMadeService.API.DTOs;
using PaymentMadeService.API.Models;
using PaymentMadeService.API.Services;

namespace PaymentMadeService.API.Controllers
{
    [ApiController]
    [Route("api/payments-made")]
    public class PaymentsMadeController : ControllerBase
    {
        private readonly IPaymentMadeService _service;
        private readonly PaymentMadeDbContext _context;
        private readonly ILogger<PaymentsMadeController> _logger;

        public PaymentsMadeController(
            IPaymentMadeService service,
            PaymentMadeDbContext context,
            ILogger<PaymentsMadeController> logger)
        {
            _service = service;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] PaymentMadeQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var (data, total) = await _service.GetPagedAsync(
                    query,
                    cancellationToken);

                return Ok(new
                {
                    data,
                    total,
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
                var payment = await _service.GetByIdAsync(id, cancellationToken);

                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found." });
                }

                return Ok(payment);
            });
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] CreatePaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var payment = await _service.CreateAsync(dto, cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = payment.Id },
                    payment);
            });
        }

        [HttpPost("{id:int}/void")]
        public Task<IActionResult> Void(
            [Range(1, int.MaxValue)] int id,
            [FromBody] VoidPaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var payment = await _service.VoidAsync(id, dto, cancellationToken);

                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found." });
                }

                return Ok(payment);
            });
        }

        [HttpGet("bills/{billId:int}/balance")]
        public Task<IActionResult> BillBalance(
            [Range(1, int.MaxValue)] int billId,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var balance = await _service.GetBillBalanceAsync(
                    billId,
                    cancellationToken);

                if (balance == null)
                {
                    return NotFound(new { message = "Bill not found." });
                }

                return Ok(balance);
            });
        }

        [HttpGet("summary")]
        public Task<IActionResult> Summary(CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var query = _context.PaymentsMade.AsNoTracking();

                var total = await query.CountAsync(cancellationToken);

                var recorded = await query.CountAsync(
                    row => row.Status == PaymentMadeStatus.Recorded,
                    cancellationToken);

                var byCurrency = await query
                    .Where(row => row.Status == PaymentMadeStatus.Recorded)
                    .GroupBy(row => new
                    {
                        row.Currency,
                        row.CurrencyDecimalPlaces
                    })
                    .Select(group => new
                    {
                        group.Key.Currency,
                        group.Key.CurrencyDecimalPlaces,
                        Amount = group.Sum(row => row.Amount)
                    })
                    .OrderBy(row => row.Currency)
                    .ToListAsync(cancellationToken);

                return Ok(new
                {
                    total,
                    recorded,
                    voided = total - recorded,
                    byCurrency
                });
            });
        }

        [HttpGet("lookups/payment-modes")]
        public Task<IActionResult> PaymentModes(CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var data = await _context.PaymentModes
                    .AsNoTracking()
                    .Where(row => row.IsActive)
                    .OrderBy(row => row.Name)
                    .Select(row => new { row.Id, row.Name })
                    .ToListAsync(cancellationToken);

                return Ok(data);
            });
        }

        [HttpGet("lookups/vendors")]
        public Task<IActionResult> Vendors(
            CancellationToken cancellationToken,
            [FromQuery, MaxLength(200)] string? search = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 50)
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
                    ? new List<PaymentVendorLookup>()
                    : await query
                        .OrderBy(row => row.Name)
                        .ThenBy(row => row.Id)
                        .Skip((int)offset)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                return Ok(new { data, total, page, pageSize });
            });
        }

        [HttpGet("lookups/bills")]
        public Task<IActionResult> Bills(
            [FromQuery] Guid vendorId,
            CancellationToken cancellationToken,
            [FromQuery, MaxLength(200)] string? search = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 50)
        {
            return HandleAsync(async () =>
            {
                if (vendorId == Guid.Empty)
                {
                    return BadRequest(new { message = "Select a vendor." });
                }

                var query = _context.Bills
                    .AsNoTracking()
                    .Where(row => row.VendorId == vendorId && row.Status == "Issued");

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var text = search.Trim();

                    query = query.Where(row =>
                        (row.BillNumber != null && row.BillNumber.Contains(text)) ||
                        row.VendorInvoiceNumber.Contains(text));
                }

                var balances = query.Select(bill => new BillPaymentBalanceDto
                {
                    Id = bill.Id,
                    VendorId = bill.VendorId,
                    VendorName = bill.VendorName,
                    BillNumber = bill.BillNumber,
                    VendorInvoiceNumber = bill.VendorInvoiceNumber,
                    Branch = bill.Branch,
                    Currency = bill.Currency,
                    CurrencyDecimalPlaces = bill.CurrencyDecimalPlaces,
                    BillDate = bill.BillDate,
                    Status = bill.Status,
                    GrandTotal = bill.GrandTotal,
                    RowVersion = bill.RowVersion,
                    AppliedCreditAmount = _context.CreditApplications
                        .Where(row => row.BillId == bill.Id && row.Status == "Applied")
                        .Sum(row => (decimal?)row.Amount) ?? 0m,
                    PaidAmount = _context.PaymentsMade
                        .Where(payment =>
                            payment.BillId == bill.Id &&
                            payment.Status == PaymentMadeStatus.Recorded)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m
                })
                    .Where(row => row.GrandTotal > row.PaidAmount + row.AppliedCreditAmount);

                var total = await balances.CountAsync(cancellationToken);
                var offset = ((long)page - 1) * pageSize;

                var data = offset >= total
                    ? new List<BillPaymentBalanceDto>()
                    : await balances
                        .OrderBy(row => row.BillDate)
                        .ThenBy(row => row.Id)
                        .Skip((int)offset)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                return Ok(new { data, total, page, pageSize });
            });
        }

        private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "This record changed. Refresh before trying again."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Unable to save payment.");

                return Problem(
                    statusCode: 500,
                    title: "Unable to save payment changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Payment database unavailable.");

                return Problem(
                    statusCode: 503,
                    title: "Payment database is temporarily unavailable.");
            }
        }
    }
}
