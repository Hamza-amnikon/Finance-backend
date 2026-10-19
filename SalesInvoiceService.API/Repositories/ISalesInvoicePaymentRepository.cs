using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Repositories
{
    public interface ISalesInvoicePaymentRepository
    {
        Task<SalesInvoicePayment?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<List<SalesInvoicePayment>> GetByInvoiceIdAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken);

        Task<decimal> GetIssuedCreditAmountAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken);

        Task<(
            List<SalesInvoicePayment> Items,
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
                CancellationToken cancellationToken);

        Task<SalesInvoicePayment> CreateAsync(
            SalesInvoicePayment payment,
            CancellationToken cancellationToken);
    }
}