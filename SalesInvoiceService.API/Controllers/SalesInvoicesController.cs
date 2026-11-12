using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-invoices")]
    public class SalesInvoicesController : ControllerBase
    {
        private readonly ICurrencyService _currencyService;
        private readonly InvoiceTaxSettingsService _taxSettings;
        private readonly ISalesInvoiceCalculator _calculator;
        private readonly ISalesInvoiceService _invoiceService;

        public SalesInvoicesController(
            ICurrencyService currencyService,
            ISalesInvoiceCalculator calculator,
            ISalesInvoiceService invoiceService,
            InvoiceTaxSettingsService taxSettings)
        {
            _currencyService = currencyService;
            _taxSettings = taxSettings;
            _calculator = calculator;
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            CancellationToken cancellationToken,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new
                {
                    message =
                        "Page must be at least 1. " +
                        "Page size must be between 1 and 100."
                });
            }

            try
            {
                var result = await _invoiceService.GetAllAsync(
                    page,
                    pageSize,
                    cancellationToken);

                return Ok(new
                {
                    data = result.Data,
                    total = result.Total,
                    page,
                    pageSize
                });
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }

        [HttpPost("preview")]
        public async Task<IActionResult> Preview(
            [FromBody] CreateSalesInvoiceDto dto,
            CancellationToken cancellationToken)
        {
            var currency = await _currencyService.GetByCodeAsync(
                dto.Currency,
                cancellationToken);

            if (currency == null)
            {
                return BadRequest(new
                {
                    message = "Select an active currency."
                });
            }

            if (dto.Items == null ||
                dto.Items.Count == 0 ||
                dto.Items.Any(item => item == null))
            {
                return BadRequest(new
                {
                    message = "Add at least one valid service line."
                });
            }

            var invoice = new SalesInvoice
            {
                ClientId = dto.ClientId,
                InvoiceDate = dto.InvoiceDate,
                DueDate = dto.DueDate,
                Currency = currency.CurrencyCode,
                Branch = dto.Branch.Trim(),
                Status = SalesInvoiceStatus.Draft,
                Items = dto.Items.Select(item =>
                    new SalesInvoiceItem
                    {
                        ServiceName = item.ServiceName.Trim(),
                        Description = item.Description?.Trim()
                            ?? string.Empty,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountType = item.DiscountType,
                        DiscountValue = item.DiscountValue,
                        DiscountAmount = item.DiscountEnabled
                            ? item.DiscountAmount
                            : 0m
                    }).ToList()
            };

            try
            {
                await _taxSettings.ApplyAsync(invoice, dto.Items, cancellationToken);
                _calculator.Calculate(
                    invoice,
                    currency.DecimalPlaces);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (OverflowException)
            {
                return BadRequest(new
                {
                    message = "Invoice amounts exceed the supported range."
                });
            }

            return Ok(new
            {
                currency = invoice.Currency,
                subTotal = invoice.SubTotal,
                discountTotal = invoice.DiscountTotal,
                taxTotal = invoice.TaxTotal,
                grandTotal = invoice.GrandTotal,
                items = invoice.Items.Select(item => new
                {
                    lineNumber = item.LineNumber,
                    serviceName = item.ServiceName,
                    unit = item.Unit,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice,
                    discountEnabled = item.DiscountEnabled,
                    discountAmount = item.DiscountAmount,
                    taxRate = item.TaxRate,
                    lineSubTotal = item.LineSubTotal,
                    taxAmount = item.TaxAmount,
                    lineTotal = item.LineTotal
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateSalesInvoiceDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new
                    {
                        id = invoice.Id
                    },
                    invoice);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (OverflowException)
            {
                return BadRequest(new
                {
                    message = "Invoice amounts exceed the supported range."
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "ClientService is unavailable or returned an error. " +
                            "Try again later."
                    });
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    new
                    {
                        message =
                            "ClientService took too long to respond. " +
                            "Try again later."
                    });
            }
        }

        [HttpPost("{id:int}/issue")]
        public async Task<IActionResult> Issue(
            int id,
            [FromBody] IssueSalesInvoiceDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.IssueAsync(id, dto, cancellationToken);
                return invoice == null
                    ? NotFound(new
                    {
                        message = "Invoice not found."
                    })
                    : Ok(invoice);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "Invoice changed since you opened it. Reload it before issuing."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
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
            catch (OverflowException)
            {
                return BadRequest(new
                {
                    message = "Invoice amounts exceed the supported range."
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(503, new
                {
                    message =
                        "A required finance service is unavailable. " +
                        "The invoice may already be issued; try issuing it again to complete the accounting posting."
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(504, new
                {
                    message =
                        "A required finance service took too long to respond. " +
                        "Try issuing the invoice again."
                });
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.GetByIdAsync(
                    id,
                    cancellationToken);

                if (invoice == null)
                {
                    return NotFound(new
                    {
                        message = "Invoice not found."
                    });
                }

                return Ok(invoice);
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }
    }
}
