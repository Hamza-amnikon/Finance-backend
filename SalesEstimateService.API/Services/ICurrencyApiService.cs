using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public interface ICurrencyApiService
    {
        Task<EstimateCurrencyDto?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken);
    }
}
