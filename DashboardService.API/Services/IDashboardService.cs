using DashboardService.API.DTOs;

namespace DashboardService.API.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetSummaryAsync(
            DateOnly from,
            DateOnly to,
            string branch,
            string currency,
            CancellationToken cancellationToken);

        Task<List<DashboardActivityDto>> GetActivityAsync(
            CancellationToken cancellationToken,
            string? branch = null);
    }
}
