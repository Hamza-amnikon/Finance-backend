using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

namespace SalesInvoiceService.API.Services
{
    public class SalesInvoicePaymentService
        : ISalesInvoicePaymentService
    {
        private readonly ISalesInvoiceRepository _invoiceRepository;
        private readonly ISalesInvoicePaymentRepository _paymentRepository;
        private readonly IPaymentModeService _paymentModeService;
        private readonly ICurrencyService _currencyService;

        public SalesInvoicePaymentService(
            ISalesInvoiceRepository invoiceRepository,
            ISalesInvoicePaymentRepository paymentRepository,
            IPaymentModeService paymentModeService,
            ICurrencyService currencyService)
        {
            _invoiceRepository = invoiceRepository;
            _paymentRepository = paymentRepository;
            _paymentModeService = paymentModeService;
            _currencyService = currencyService;
        }

        public async Task<SalesInvoicePaymentResponseDto> CreateAsync(
            int salesInvoiceId,
            CreateSalesInvoicePaymentDto dto,
            CancellationToken cancellationToken)
        {
            if (dto.RequestId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A payment request ID is required.");
            }

            if (dto.PaymentDate == default)
            {
                throw new ArgumentException(
                    "Payment date is required.");
            }

            if (dto.Amount <= 0 ||
                dto.Amount > 99999999999999.9999m)
            {
                throw new ArgumentException(
                    "Enter a positive payment amount within the supported range.");
            }

            var paymentModeName = Clean(dto.PaymentMode);
            var reference = Clean(dto.Reference);
            var notes = Clean(dto.Notes);

            // Return the original payment when the same submission is retried.
            var existing = await _paymentRepository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                if (existing.SalesInvoiceId != salesInvoiceId ||
                    existing.PaymentDate != dto.PaymentDate ||
                    existing.Amount != dto.Amount ||
                    !string.Equals(
                        existing.PaymentMode,
                        paymentModeName,
                        StringComparison.OrdinalIgnoreCase) ||
                    existing.Reference != reference ||
                    existing.Notes != notes)
                {
                    throw new InvalidOperationException(
                        "This request ID was already used for different payment details.");
                }

                return ToResponse(existing);
            }

            var invoice = await _invoiceRepository.GetByIdAsync(
                salesInvoiceId,
                cancellationToken);

            if (invoice == null)
            {
                throw new KeyNotFoundException(
                    "Invoice not found.");
            }

            if (invoice.Status != SalesInvoiceStatus.Issued)
            {
                throw new ArgumentException(
                    "Payments can only be recorded against issued invoices.");
            }

            var currency = await _currencyService.GetByCodeAsync(
                invoice.Currency,
                cancellationToken);

            if (currency == null)
            {
                throw new ArgumentException(
                    "The invoice currency is unavailable or inactive.");
            }

            if (currency.DecimalPlaces < 0 ||
                currency.DecimalPlaces > 4)
            {
                throw new InvalidOperationException(
                    "The currency decimal places are not configured correctly.");
            }

            if (decimal.Round(dto.Amount, currency.DecimalPlaces) !=
                dto.Amount)
            {
                throw new ArgumentException(
                    $"{invoice.Currency} payments support up to " +
                    $"{currency.DecimalPlaces} decimal places.");
            }

            var paymentMode = await _paymentModeService.GetByNameAsync(
                paymentModeName,
                cancellationToken);

            if (paymentMode == null)
            {
                throw new ArgumentException(
                    "Select an active payment mode.");
            }

            var payment = new SalesInvoicePayment
            {
                SalesInvoiceId = invoice.Id,
                RequestId = dto.RequestId,
                PaymentDate = dto.PaymentDate,
                Amount = dto.Amount,
                Currency = invoice.Currency,
                PaymentMode = paymentMode.Name,
                Reference = reference,
                Notes = notes
            };

            // The repository checks the latest balance under a database lock.
            var created = await _paymentRepository.CreateAsync(
                payment,
                cancellationToken);

            return ToResponse(created);
        }

        public async Task<(
            List<SalesInvoicePaymentResponseDto> Items,
            int Total)> GetPagedAsync(
                int page,
                int pageSize,
                string? search,
                DateOnly? fromDate,
                DateOnly? toDate,
                Guid? clientId,
                string? branch,
                string? currency,
                string? paymentMode,
                CancellationToken cancellationToken)
        {
            var result = await _paymentRepository.GetPagedAsync(
                page,
                pageSize,
                search,
                fromDate,
                toDate,
                clientId,
                branch,
                currency,
                paymentMode,
                cancellationToken);

            var items = result.Items
                .Select(ToResponse)
                .ToList();

            return (items, result.Total);
        }

        public async Task<SalesInvoicePaymentSummaryDto?> GetSummaryAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(
                salesInvoiceId,
                cancellationToken);

            if (invoice == null)
            {
                return null;
            }

            var payments = await _paymentRepository.GetByInvoiceIdAsync(
                salesInvoiceId,
                cancellationToken);

            var paidAmount = payments.Sum(payment => payment.Amount);

            var creditedAmount =
                await _paymentRepository.GetIssuedCreditAmountAsync(
                    salesInvoiceId,
                    cancellationToken);

            if (paidAmount < 0 || paidAmount > invoice.GrandTotal)
            {
                throw new InvalidOperationException(
                    "Recorded payments exceed the invoice total. Review the payment history.");
            }

            if (creditedAmount < 0 ||
                creditedAmount > invoice.GrandTotal)
            {
                throw new InvalidOperationException(
                    "Issued credits exceed the invoice total. Review the credit notes.");
            }

            var adjustedTotal =
                invoice.GrandTotal - creditedAmount;

            var balanceDue =
                Math.Max(0m, adjustedTotal - paidAmount);

            var creditBalance =
                Math.Max(0m, paidAmount - adjustedTotal);

            var paymentStatus = creditBalance > 0
                ? "Credit Balance"
                : creditedAmount > 0 && adjustedTotal == 0
                    ? "Credited"
                    : balanceDue == 0
                        ? "Paid"
                        : paidAmount > 0
                            ? "Partially Paid"
                            : "Unpaid";

            return new SalesInvoicePaymentSummaryDto
            {
                SalesInvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                Currency = invoice.Currency,
                InvoiceTotal = invoice.GrandTotal,
                CreditedAmount = creditedAmount,
                AdjustedInvoiceTotal = adjustedTotal,
                CreditBalance = creditBalance,
                PaidAmount = paidAmount,
                BalanceDue = balanceDue,
                PaymentStatus = paymentStatus,
                Payments = payments
                    .Select(ToResponse)
                    .ToList()
            };
        }

        private static SalesInvoicePaymentResponseDto ToResponse(
            SalesInvoicePayment payment)
        {
            return new SalesInvoicePaymentResponseDto
            {
                Id = payment.Id,
                SalesInvoiceId = payment.SalesInvoiceId,
                RequestId = payment.RequestId,
                PaymentNumber = $"PAY-{payment.Id:D6}",
                InvoiceNumber =
                    payment.SalesInvoice.InvoiceNumber ?? string.Empty,
                ClientId = payment.SalesInvoice.ClientId,
                ClientName = payment.SalesInvoice.ClientName,
                Branch = payment.SalesInvoice.Branch,
                PaymentDate = payment.PaymentDate,
                Amount = payment.Amount,
                Currency = payment.Currency,
                PaymentMode = payment.PaymentMode,
                Reference = payment.Reference,
                Notes = payment.Notes,
                CreatedDate = payment.CreatedDate
            };
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}