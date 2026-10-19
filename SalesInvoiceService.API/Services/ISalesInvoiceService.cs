using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface ISalesInvoiceService
    {
        Task<SalesInvoiceResponseDto> CreateAsync(
            CreateSalesInvoiceDto dto,
            CancellationToken cancellationToken);

        Task<SalesInvoiceResponseDto?> IssueAsync(
            int id,
            IssueSalesInvoiceDto dto,
            CancellationToken cancellationToken);

        Task<SalesInvoiceResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<(List<SalesInvoiceResponseDto> Data, int Total)>
            GetAllAsync(
                int page,
                int pageSize,
                CancellationToken cancellationToken);
    }
}