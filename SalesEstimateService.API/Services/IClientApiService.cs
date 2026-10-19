using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public interface IClientApiService
    {
        Task<EstimateClientDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);
    }
}
