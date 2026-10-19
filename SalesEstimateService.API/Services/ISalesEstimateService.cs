using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public interface ISalesEstimateService
    {
        Task<SalesEstimateResponseDto> CreateAsync(
            CreateSalesEstimateDto dto,
            CancellationToken cancellationToken);

        Task<SalesEstimateResponseDto> PreviewAsync(
            CreateSalesEstimateDto dto,
            CancellationToken cancellationToken);

        Task<SalesEstimateResponseDto?> MarkAsSentAsync(
            int id,
            SalesEstimateActionDto dto,
            CancellationToken cancellationToken);

        Task<SalesEstimateResponseDto?> AcceptAsync(
            int id,
            SalesEstimateActionDto dto,
            CancellationToken cancellationToken);

        Task<SalesEstimateResponseDto?> DeclineAsync(
            int id,
            SalesEstimateActionDto dto,
            CancellationToken cancellationToken);

        Task<SalesEstimateResponseDto?> UpdateAsync(
            int id,
            UpdateSalesEstimateDto dto,
            CancellationToken cancellationToken);

        Task<SalesEstimateResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<(List<SalesEstimateResponseDto> Data, int Total)> GetAllAsync(
            SalesEstimateQueryDto query,
            CancellationToken cancellationToken);
    }
}
