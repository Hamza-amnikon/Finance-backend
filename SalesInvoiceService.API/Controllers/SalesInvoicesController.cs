using Microsoft.AspNetCore.Mvc;
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
        private readonly ISalesInvoiceCalculator _calculator;
        private readonly ISalesInvoiceService _invoiceService;

        public SalesInvoicesController(
            ICurrencyService currencyService,
            ISalesInvoiceCalculator calculator,
            ISalesInvoiceService invoiceService)
        {
            _currencyService = currencyService;
            _calculator = calculator;
            _invoiceService = invoiceService;
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
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate
                    }).ToList()
            };

            try
            {
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
                    new { id = invoice.Id },
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

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
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
    }
}