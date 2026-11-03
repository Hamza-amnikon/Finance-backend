using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface ITrialBalanceService
    {
        Task<TrialBalanceResultDto>
            GetAsync(
                TrialBalanceQueryDto query,
                CancellationToken cancellationToken);
    }
}