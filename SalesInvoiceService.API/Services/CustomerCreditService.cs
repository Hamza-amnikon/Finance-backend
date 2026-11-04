using System.Data;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public class CustomerCreditService
        : ICustomerCreditService
    {
        private readonly SalesInvoiceDbContext _context;

        public CustomerCreditService(
            SalesInvoiceDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerCreditDto> GetAsync(
            Guid clientId,
            string branch,
            string currency,
            CancellationToken cancellationToken)
        {
            if (clientId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Select a client.");
            }

            branch = branch?.Trim() ?? string.Empty;
            currency = currency?.Trim().ToUpperInvariant()
                ?? string.Empty;

            if (branch.Length == 0 || currency.Length != 3)
            {
                throw new ArgumentException(
                    "Select a branch and three-letter currency.");
            }

            var invoices = await _context.SalesInvoices
                .AsNoTracking()
                .Where(invoice =>
                    invoice.ClientId == clientId &&
                    invoice.Branch == branch &&
                    invoice.Currency == currency &&
                    invoice.Status == SalesInvoiceStatus.Issued)
                .OrderBy(invoice => invoice.Id)
                .Select(invoice => new
                {
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.ClientName,
                    invoice.GrandTotal
                })
                .ToListAsync(cancellationToken);

            var result = new CustomerCreditDto
            {
                ClientId = clientId,
                ClientName = invoices.FirstOrDefault()?.ClientName
                    ?? string.Empty,
                Branch = branch,
                Currency = currency
            };

            if (invoices.Count == 0)
            {
                return result;
            }

            var invoiceIds = invoices
                .Select(invoice => invoice.Id)
                .ToList();

            var payments = await _context.SalesInvoicePayments
                .AsNoTracking()
                .Where(payment =>
                    invoiceIds.Contains(payment.SalesInvoiceId))
                .GroupBy(payment => payment.SalesInvoiceId)
                .Select(group => new
                {
                    InvoiceId = group.Key,
                    Amount = group.Sum(payment => payment.Amount)
                })
                .ToDictionaryAsync(
                    row => row.InvoiceId,
                    row => row.Amount,
                    cancellationToken);

            var credits = await _context.SalesCreditNotes
                .AsNoTracking()
                .Where(credit =>
                    invoiceIds.Contains(credit.SalesInvoiceId) &&
                    credit.Status == SalesCreditNoteStatus.Issued)
                .Select(credit => new
                {
                    credit.SalesInvoiceId,
                    credit.CreditNoteNumber,
                    credit.GrandTotal
                })
                .ToListAsync(cancellationToken);

            var applications = await _context
                .CustomerCreditApplications
                .AsNoTracking()
                .Where(application =>
                    invoiceIds.Contains(
                        application.SourceInvoiceId) ||
                    invoiceIds.Contains(
                        application.TargetInvoiceId))
                .OrderByDescending(application =>
                    application.AppliedAtUtc)
                .ThenByDescending(application =>
                    application.Id)
                .ToListAsync(cancellationToken);

            var availableByInvoiceId =
                new Dictionary<int, decimal>();

            var dueByInvoiceId =
                new Dictionary<int, decimal>();

            foreach (var invoice in invoices)
            {
                var paid = payments.GetValueOrDefault(invoice.Id);

                var invoiceCredits = credits
                    .Where(credit =>
                        credit.SalesInvoiceId == invoice.Id)
                    .ToList();

                var credited = invoiceCredits.Sum(
                    credit => credit.GrandTotal);

                var appliedFromOtherInvoices = applications
                    .Where(application =>
                        application.TargetInvoiceId ==
                            invoice.Id)
                    .Sum(application =>
                        application.Amount);

                var appliedToOtherInvoices = applications
                    .Where(application =>
                        application.SourceInvoiceId ==
                            invoice.Id)
                    .Sum(application =>
                        application.Amount);

                if (paid < 0m ||
                    paid > invoice.GrandTotal ||
                    credited < 0m ||
                    credited > invoice.GrandTotal)
                {
                    throw new InvalidOperationException(
                        "Invoice payments or issued credits exceed the invoice total. Review the invoice history.");
                }

                var available = Math.Max(
                    0m,
                    paid +
                    credited +
                    appliedFromOtherInvoices -
                    invoice.GrandTotal -
                    appliedToOtherInvoices);

                availableByInvoiceId[invoice.Id] =
                    available;

                dueByInvoiceId[invoice.Id] =
                    Math.Max(
                        0m,
                        invoice.GrandTotal -
                        credited -
                        paid -
                        appliedFromOtherInvoices);

                if (dueByInvoiceId[invoice.Id] > 0m)
                {
                    result.Targets.Add(
                        new CustomerCreditTargetDto
                        {
                            SalesInvoiceId =
                                invoice.Id,
                            InvoiceNumber =
                                invoice.InvoiceNumber ??
                                    string.Empty,
                            BalanceDue =
                                dueByInvoiceId[
                                    invoice.Id]
                        });
                }

                if (available == 0m)
                {
                    continue;
                }

                result.Sources.Add(
                    new CustomerCreditSourceDto
                    {
                        SalesInvoiceId = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber
                            ?? string.Empty,
                        PaidAmount = paid,
                        CreditedAmount = credited,
                        AppliedFromOtherInvoices =
                            appliedFromOtherInvoices,
                        AppliedToOtherInvoices =
                            appliedToOtherInvoices,
                        AvailableAmount = available,
                        CreditNoteNumbers = invoiceCredits
                            .Select(credit =>
                                credit.CreditNoteNumber)
                            .Where(number =>
                                !string.IsNullOrWhiteSpace(number))
                            .Select(number => number!)
                            .OrderBy(number => number)
                            .ToList()
                    });

                result.TotalAvailable += available;
            }

            result.Applications = applications
                .Select(application => ToResponse(
                    application,
                    invoices.First(invoice =>
                        invoice.Id ==
                            application.SourceInvoiceId)
                        .InvoiceNumber ?? string.Empty,
                    invoices.First(invoice =>
                        invoice.Id ==
                            application.TargetInvoiceId)
                        .InvoiceNumber ?? string.Empty,
                    availableByInvoiceId[
                        application.SourceInvoiceId],
                    dueByInvoiceId[
                        application.TargetInvoiceId]))
                .ToList();

            return result;
        }

        public async Task<CustomerCreditApplicationDto> ApplyAsync(
            ApplyCustomerCreditDto dto,
            CancellationToken cancellationToken)
        {
            if (dto.RequestId == Guid.Empty ||
                dto.SourceInvoiceId < 1 ||
                dto.TargetInvoiceId < 1 ||
                dto.SourceInvoiceId == dto.TargetInvoiceId ||
                dto.Amount <= 0m ||
                dto.Amount > 99999999999999.9999m ||
                decimal.Round(dto.Amount, 4) != dto.Amount)
            {
                throw new ArgumentException(
                    "Select two different invoices and enter a positive amount with no more than four decimal places.");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            var firstId = Math.Min(
                dto.SourceInvoiceId,
                dto.TargetInvoiceId);

            var secondId = Math.Max(
                dto.SourceInvoiceId,
                dto.TargetInvoiceId);

            var first = await LockInvoiceAsync(
                firstId,
                cancellationToken);

            var second = await LockInvoiceAsync(
                secondId,
                cancellationToken);

            if (first == null || second == null)
            {
                throw new KeyNotFoundException(
                    "Source or target invoice was not found.");
            }

            var source = first.Id == dto.SourceInvoiceId
                ? first
                : second;

            var target = first.Id == dto.TargetInvoiceId
                ? first
                : second;

            if (source.Status != SalesInvoiceStatus.Issued ||
                target.Status != SalesInvoiceStatus.Issued ||
                source.ClientId != target.ClientId ||
                source.Branch != target.Branch ||
                source.Currency != target.Currency)
            {
                throw new ArgumentException(
                    "Both invoices must be issued for the same client, branch and currency.");
            }

            var existing = await _context
                .CustomerCreditApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    application =>
                        application.RequestId ==
                            dto.RequestId,
                    cancellationToken);

            if (existing != null)
            {
                if (existing.SourceInvoiceId !=
                        dto.SourceInvoiceId ||
                    existing.TargetInvoiceId !=
                        dto.TargetInvoiceId ||
                    existing.Amount != dto.Amount)
                {
                    throw new InvalidOperationException(
                        "This request ID was used for a different credit application.");
                }

                var existingBalances =
                    await GetBalancesAsync(
                        source,
                        target,
                        cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return ToResponse(
                    existing,
                    source.InvoiceNumber ?? string.Empty,
                    target.InvoiceNumber ?? string.Empty,
                    existingBalances.SourceCredit,
                    existingBalances.TargetDue);
            }

            var balances = await GetBalancesAsync(
                source,
                target,
                cancellationToken);

            if (dto.Amount > balances.SourceCredit)
            {
                throw new InvalidOperationException(
                    "The source invoice does not have enough unused customer credit.");
            }

            if (dto.Amount > balances.TargetDue)
            {
                throw new InvalidOperationException(
                    "The credit amount exceeds the target invoice balance due.");
            }

            var application =
                new CustomerCreditApplication
                {
                    RequestId = dto.RequestId,
                    SourceInvoiceId =
                        source.Id,
                    TargetInvoiceId =
                        target.Id,
                    ClientId = source.ClientId,
                    Branch = source.Branch,
                    Currency = source.Currency,
                    Amount = dto.Amount,
                    AppliedAtUtc = DateTime.UtcNow
                };

            _context.CustomerCreditApplications
                .Add(application);

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return ToResponse(
                application,
                source.InvoiceNumber ?? string.Empty,
                target.InvoiceNumber ?? string.Empty,
                balances.SourceCredit - dto.Amount,
                balances.TargetDue - dto.Amount);
        }

        private async Task<SalesInvoice?> LockInvoiceAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.SalesInvoices
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM [sales].[SalesInvoices] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {id}
                    """)
                .FirstOrDefaultAsync(
                    cancellationToken);
        }

        private async Task<(
            decimal SourceCredit,
            decimal TargetDue)> GetBalancesAsync(
            SalesInvoice source,
            SalesInvoice target,
            CancellationToken cancellationToken)
        {
            var sourcePaid = await _context
                .SalesInvoicePayments
                .Where(payment =>
                    payment.SalesInvoiceId ==
                        source.Id)
                .SumAsync(
                    payment => (decimal?)payment.Amount,
                    cancellationToken) ?? 0m;

            var sourceCredited = await _context
                .SalesCreditNotes
                .Where(credit =>
                    credit.SalesInvoiceId ==
                        source.Id &&
                    credit.Status ==
                        SalesCreditNoteStatus.Issued)
                .SumAsync(
                    credit => (decimal?)credit.GrandTotal,
                    cancellationToken) ?? 0m;

            var sourceIncoming = await _context
                .CustomerCreditApplications
                .Where(application =>
                    application.TargetInvoiceId ==
                        source.Id)
                .SumAsync(
                    application =>
                        (decimal?)application.Amount,
                    cancellationToken) ?? 0m;

            var sourceOutgoing = await _context
                .CustomerCreditApplications
                .Where(application =>
                    application.SourceInvoiceId ==
                        source.Id)
                .SumAsync(
                    application =>
                        (decimal?)application.Amount,
                    cancellationToken) ?? 0m;

            var targetPaid = await _context
                .SalesInvoicePayments
                .Where(payment =>
                    payment.SalesInvoiceId ==
                        target.Id)
                .SumAsync(
                    payment => (decimal?)payment.Amount,
                    cancellationToken) ?? 0m;

            var targetCredited = await _context
                .SalesCreditNotes
                .Where(credit =>
                    credit.SalesInvoiceId ==
                        target.Id &&
                    credit.Status ==
                        SalesCreditNoteStatus.Issued)
                .SumAsync(
                    credit => (decimal?)credit.GrandTotal,
                    cancellationToken) ?? 0m;

            var targetIncoming = await _context
                .CustomerCreditApplications
                .Where(application =>
                    application.TargetInvoiceId ==
                        target.Id)
                .SumAsync(
                    application =>
                        (decimal?)application.Amount,
                    cancellationToken) ?? 0m;

            var sourceCredit = Math.Max(
                0m,
                sourcePaid +
                sourceCredited +
                sourceIncoming -
                source.GrandTotal -
                sourceOutgoing);

            var targetDue = Math.Max(
                0m,
                target.GrandTotal -
                targetCredited -
                targetPaid -
                targetIncoming);

            return (sourceCredit, targetDue);
        }

        private static CustomerCreditApplicationDto
            ToResponse(
                CustomerCreditApplication application,
                string sourceInvoiceNumber,
                string targetInvoiceNumber,
                decimal sourceCreditRemaining,
                decimal targetBalanceDue)
        {
            return new CustomerCreditApplicationDto
            {
                Id = application.Id,
                RequestId = application.RequestId,
                SourceInvoiceId =
                    application.SourceInvoiceId,
                SourceInvoiceNumber =
                    sourceInvoiceNumber,
                TargetInvoiceId =
                    application.TargetInvoiceId,
                TargetInvoiceNumber =
                    targetInvoiceNumber,
                ClientId = application.ClientId,
                Branch = application.Branch,
                Currency = application.Currency,
                Amount = application.Amount,
                SourceCreditRemaining =
                    sourceCreditRemaining,
                TargetBalanceDue =
                    targetBalanceDue,
                AppliedAtUtc =
                    application.AppliedAtUtc
            };
        }
    }
}
