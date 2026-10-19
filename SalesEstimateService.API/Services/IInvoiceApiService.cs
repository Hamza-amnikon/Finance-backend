using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public interface IInvoiceApiService
    {
        Task<ConvertedInvoiceDto> CreateFromEstimateAsync(
            int estimateId,
            ConvertSalesEstimateDto dto,
            CancellationToken cancellationToken);

        Task<ConvertedInvoiceDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    }
}
