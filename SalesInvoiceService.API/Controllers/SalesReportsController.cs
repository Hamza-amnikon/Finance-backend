using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/sales-reports")]
    public class SalesReportsController : ControllerBase
    {
        private readonly SalesInvoiceDbContext _context;

        public SalesReportsController(SalesInvoiceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateOnly fromDate,
            [FromQuery] DateOnly toDate,
            CancellationToken cancellationToken)
        {
            if (fromDate == default || toDate == default || fromDate > toDate ||
                toDate.DayNumber - fromDate.DayNumber > 3660)
            {
                return BadRequest(new { message = "Choose a valid date range of up to ten years." });
            }

            // Read-only reporting: no changes to document, payment or posting workflows.
            var invoices = await _context.SalesInvoices.AsNoTracking()
                .Where(row => row.Status == SalesInvoiceStatus.Issued && row.InvoiceDate <= toDate)
                .Select(row => new
                {
                    row.Id, row.InvoiceNumber, row.ClientId, row.ClientName,
                    row.Branch, row.Currency, row.InvoiceDate, row.DueDate,
                    row.GrandTotal, row.TaxTotal,
                    Services = row.Items.Select(item => new
                    {
                        item.ServiceName, Net = item.LineTotal - item.TaxAmount
                    }).ToList()
                }).ToListAsync(cancellationToken);
            var credits = await _context.SalesCreditNotes.AsNoTracking()
                .Where(row => row.Status == SalesCreditNoteStatus.Issued &&
                    row.CreditNoteDate <= toDate &&
                    row.SalesInvoice.Status == SalesInvoiceStatus.Issued &&
                    row.SalesInvoice.InvoiceDate <= toDate)
                .Select(row => new
                {
                    row.Id, row.SalesInvoiceId, row.CreditNoteNumber,
                    row.CreditNoteDate, row.GrandTotal, row.TaxTotal,
                    Services = row.Items.Select(item => new
                    {
                        item.ServiceName, Net = item.LineTotal - item.TaxAmount
                    }).ToList()
                }).ToListAsync(cancellationToken);
            var payments = await _context.SalesInvoicePayments.AsNoTracking()
                .Where(row => row.PaymentDate <= toDate &&
                    row.SalesInvoice.Status == SalesInvoiceStatus.Issued &&
                    row.SalesInvoice.InvoiceDate <= toDate)
                .Select(row => new { row.SalesInvoiceId, row.PaymentDate, row.Amount })
                .ToListAsync(cancellationToken);
            var applications = await _context.CustomerCreditApplications.AsNoTracking()
                .Where(row => row.AppliedAtUtc < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue))
                .Select(row => new { row.TargetInvoiceId, row.SourceInvoiceId, row.Amount })
                .ToListAsync(cancellationToken);
            var creditTotals = credits.GroupBy(row => row.SalesInvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.GrandTotal));
            var paidTotals = payments.GroupBy(row => row.SalesInvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
            var appliedTotals = applications.GroupBy(row => row.TargetInvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
            var transferredTotals = applications.GroupBy(row => row.SourceInvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
            var documents = new List<object>();
            foreach (var invoice in invoices.Where(row => row.InvoiceDate >= fromDate))
            {
                var credited = creditTotals.GetValueOrDefault(invoice.Id);
                var paid = paidTotals.GetValueOrDefault(invoice.Id);
                var applied = appliedTotals.GetValueOrDefault(invoice.Id);
                var adjusted = invoice.GrandTotal - credited;
                var balance = Math.Max(0m, adjusted - paid - applied);
                var creditBalance = Math.Max(0m, paid + applied - adjusted -
                    transferredTotals.GetValueOrDefault(invoice.Id));
                var status = creditBalance > 0 ? "Credit Balance" :
                    credited > 0 && adjusted == 0 ? "Credited" :
                    balance == 0 ? "Paid" : paid + applied > 0 ? "Partially Paid" : "Unpaid";
                documents.Add(new
                {
                    id = invoice.Id, type = "Invoice", number = invoice.InvoiceNumber,
                    invoice.ClientId, invoice.ClientName, invoice.Branch, invoice.Currency,
                    date = invoice.InvoiceDate, dueDate = invoice.DueDate,
                    net = invoice.GrandTotal - invoice.TaxTotal, tax = invoice.TaxTotal,
                    total = invoice.GrandTotal, paid, balance, status,
                    overdue = invoice.DueDate < toDate ? balance : 0m, invoice.Services
                });
            }
            var byId = invoices.ToDictionary(row => row.Id);
            foreach (var credit in credits.Where(row => row.CreditNoteDate >= fromDate))
            {
                var invoice = byId[credit.SalesInvoiceId];
                documents.Add(new
                {
                    id = credit.Id, type = "CreditNote", number = credit.CreditNoteNumber,
                    invoice.ClientId, invoice.ClientName, invoice.Branch, invoice.Currency,
                    date = credit.CreditNoteDate, dueDate = (DateOnly?)null,
                    net = -(credit.GrandTotal - credit.TaxTotal), tax = -credit.TaxTotal,
                    total = -credit.GrandTotal, paid = 0m, balance = 0m, overdue = 0m,
                    status = "Issued", services = credit.Services.Select(row => new
                    {
                        row.ServiceName, Net = -row.Net
                    })
                });
            }
            return Ok(new
            {
                fromDate, toDate, documents,
                receipts = payments.Where(row => row.PaymentDate >= fromDate).Select(row =>
                {
                    var invoice = byId[row.SalesInvoiceId];
                    return new { invoice.ClientId, invoice.ClientName, invoice.Branch,
                        invoice.Currency, date = row.PaymentDate, row.Amount };
                })
            });
        }
    }
}
