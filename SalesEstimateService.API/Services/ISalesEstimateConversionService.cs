using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public interface ISalesEstimateConversionService
    {
        Task<SalesEstimateConversionResponseDto?> ConvertAsync(
            int id,
            ConvertSalesEstimateDto dto,
            CancellationToken cancellationToken);
    }
}
