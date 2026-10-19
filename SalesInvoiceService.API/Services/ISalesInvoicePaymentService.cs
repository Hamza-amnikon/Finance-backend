using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface ISalesInvoicePaymentService
    {
        Task<SalesInvoicePaymentResponseDto> CreateAsync(
            int salesInvoiceId,
            CreateSalesInvoicePaymentDto dto,
            CancellationToken cancellationToken);

        Task<SalesInvoicePaymentSummaryDto?> GetSummaryAsync(
            int salesInvoiceId,
            CancellationToken cancellationToken);

        Task<(
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
                CancellationToken cancellationToken);
    }
}