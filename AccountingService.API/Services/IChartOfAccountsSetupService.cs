using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IChartOfAccountsSetupService
    {
        Task<IReadOnlyList<AccountResponseDto>>
            SetupAsync(
                int companyId,
                CancellationToken cancellationToken);
    }
}