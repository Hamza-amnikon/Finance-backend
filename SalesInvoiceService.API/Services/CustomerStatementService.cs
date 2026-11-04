using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public class CustomerStatementService : ICustomerStatementService
    {
        private readonly SalesInvoiceDbContext _context;

        public CustomerStatementService(
            SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public Task<List<CustomerStatementClientDto>> GetClientsAsync(
            CancellationToken cancellationToken)
        {
            return _context.SalesInvoices
                .AsNoTracking()
                .Where(invoice => invoice.Status == SalesInvoiceStatus.Issued)
                .Select(invoice => new CustomerStatementClientDto
                {
                    ClientId = invoice.ClientId,
                    ClientName = invoice.ClientName,
                    Branch = invoice.Branch,
                    Currency = invoice.Currency
                })
                .Distinct()
                .OrderBy(client => client.ClientName)
                .ThenBy(client => client.Branch)
                .ThenBy(client => client.Currency)
                .ToListAsync(cancellationToken);
        }

        public async Task<CustomerStatementDto> GetAsync(
            Guid clientId,
            string branch,
            string currency,
            CancellationToken cancellationToken)
        {
            if (clientId == Guid.Empty ||
                string.IsNullOrWhiteSpace(branch) ||
                string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentException(
                    "Client, branch and currency are required.");
            }

            branch = branch.Trim();
            currency = currency.Trim().ToUpperInvariant();

            var invoices = await _context.SalesInvoices
                .AsNoTracking()
                .Where(invoice =>
                    invoice.Status == SalesInvoiceStatus.Issued &&
                    invoice.ClientId == clientId &&
                    invoice.Branch == branch &&
                    invoice.Currency == currency)
                .Select(invoice => new
                {
                    invoice.Id,
                    invoice.ClientName,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate,
                    invoice.GrandTotal
                })
                .ToListAsync(cancellationToken);

            var statement = new CustomerStatementDto
            {
                ClientId = clientId,
                ClientName = invoices.FirstOrDefault()?.ClientName ?? string.Empty,
                Branch = branch,
                Currency = currency
            };

            if (invoices.Count == 0)
            {
                return statement;
            }

            var invoiceIds = invoices.Select(invoice => invoice.Id).ToList();
            var invoiceNumbers = invoices.ToDictionary(
                invoice => invoice.Id,
                invoice => invoice.InvoiceNumber ?? invoice.Id.ToString());

            var creditNotes = await _context.SalesCreditNotes
                .AsNoTracking()
                .Where(note =>
                    invoiceIds.Contains(note.SalesInvoiceId) &&
                    note.Status == SalesCreditNoteStatus.Issued)
                .Select(note => new
                {
                    note.Id,
                    note.SalesInvoiceId,
                    note.CreditNoteNumber,
                    note.CreditNoteDate,
                    note.Reason,
                    note.GrandTotal
                })
                .ToListAsync(cancellationToken);

            var payments = await _context.SalesInvoicePayments
                .AsNoTracking()
                .Where(payment => invoiceIds.Contains(payment.SalesInvoiceId))
                .Select(payment => new
                {
                    payment.Id,
                    payment.SalesInvoiceId,
                    payment.PaymentDate,
                    payment.Amount,
                    payment.PaymentMode,
                    payment.Reference
                })
                .ToListAsync(cancellationToken);

            var applications = await _context.CustomerCreditApplications
                .AsNoTracking()
                .Where(application =>
                    invoiceIds.Contains(application.SourceInvoiceId) &&
                    invoiceIds.Contains(application.TargetInvoiceId) &&
                    application.ClientId == clientId &&
                    application.Branch == branch &&
                    application.Currency == currency)
                .Select(application => new
                {
                    application.Id,
                    application.SourceInvoiceId,
                    application.TargetInvoiceId,
                    application.Amount,
                    application.AppliedAtUtc
                })
                .ToListAsync(cancellationToken);

            var lines = new List<(int Order, int Id, CustomerStatementLineDto Line)>();

            foreach (var invoice in invoices)
            {
                var number = invoiceNumbers[invoice.Id];

                lines.Add((0, invoice.Id, new CustomerStatementLineDto
                {
                    Date = invoice.InvoiceDate,
                    Type = "Invoice",
                    DocumentNumber = number,
                    Description = "Sales invoice issued",
                    InvoiceNumber = number,
                    Amount = invoice.GrandTotal,
                    BalanceChange = invoice.GrandTotal
                }));
            }

            foreach (var note in creditNotes)
            {
                lines.Add((1, note.Id, new CustomerStatementLineDto
                {
                    Date = note.CreditNoteDate,
                    Type = "CreditNote",
                    DocumentNumber = note.CreditNoteNumber ?? note.Id.ToString(),
                    Description = note.Reason,
                    InvoiceNumber = invoiceNumbers[note.SalesInvoiceId],
                    Amount = note.GrandTotal,
                    BalanceChange = -note.GrandTotal
                }));
            }

            foreach (var payment in payments)
            {
                lines.Add((2, payment.Id, new CustomerStatementLineDto
                {
                    Date = payment.PaymentDate,
                    Type = "CashPayment",
                    DocumentNumber = payment.Reference == string.Empty
                        ? $"Payment {payment.Id}"
                        : payment.Reference,
                    Description = $"Payment received by {payment.PaymentMode}",
                    InvoiceNumber = invoiceNumbers[payment.SalesInvoiceId],
                    Amount = payment.Amount,
                    BalanceChange = -payment.Amount
                }));
            }

            foreach (var application in applications)
            {
                lines.Add((3, application.Id, new CustomerStatementLineDto
                {
                    Date = DateOnly.FromDateTime(application.AppliedAtUtc),
                    Type = "CreditApplication",
                    DocumentNumber = $"Application {application.Id}",
                    Description = "Customer credit applied to another invoice",
                    InvoiceNumber = invoiceNumbers[application.SourceInvoiceId],
                    TargetInvoiceNumber = invoiceNumbers[application.TargetInvoiceId],
                    Amount = application.Amount,
                    BalanceChange = 0
                }));
            }

            decimal balance = 0;

            foreach (var item in lines
                .OrderBy(item => item.Line.Date)
                .ThenBy(item => item.Order)
                .ThenBy(item => item.Id))
            {
                balance += item.Line.BalanceChange;
                item.Line.RunningBalance = balance;
                statement.Lines.Add(item.Line);
            }

            statement.InvoiceTotal = invoices.Sum(invoice => invoice.GrandTotal);
            statement.CreditNoteTotal = creditNotes.Sum(note => note.GrandTotal);
            statement.CashPaymentTotal = payments.Sum(payment => payment.Amount);
            statement.NetBalance = balance;

            return statement;
        }
    }
}
