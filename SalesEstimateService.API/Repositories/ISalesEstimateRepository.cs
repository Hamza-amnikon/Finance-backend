using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.Repositories
{
    public interface ISalesEstimateRepository
    {
        Task<SalesEstimate> CreateAsync(
            SalesEstimate estimate,
            CancellationToken cancellationToken);

        Task<SalesEstimate?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<(List<SalesEstimate> Data, int Total)> GetAllAsync(
            int page,
            int pageSize,
            string? search,
            SalesEstimateStatus? status,
            CancellationToken cancellationToken);

        Task<SalesEstimate?> CompleteConversionAsync(
            int id,
            int invoiceId,
            byte[] rowVersion,
            CancellationToken cancellationToken);

        Task<SalesEstimate?> ChangeStatusAsync(
            int id,
            SalesEstimateStatus target,
            byte[] rowVersion,
            CancellationToken cancellationToken);

        Task<SalesEstimate?> UpdateDraftAsync(
            SalesEstimate estimate,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}
