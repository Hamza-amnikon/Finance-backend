using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface ISalesInvoicePostingService
    {
        Task<SalesInvoicePostingResponseDto> PostAsync(
            PostSalesInvoiceDto dto,
            CancellationToken cancellationToken);
    }
}
