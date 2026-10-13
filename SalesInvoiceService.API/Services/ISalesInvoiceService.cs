using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface ISalesInvoiceService
    {
        Task<SalesInvoiceResponseDto> CreateAsync(
            CreateSalesInvoiceDto dto,
            CancellationToken cancellationToken);

        Task<SalesInvoiceResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);
    }
}