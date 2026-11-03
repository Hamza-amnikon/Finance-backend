using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IGeneralLedgerService
    {
        Task<GeneralLedgerResultDto> GetAsync(
            GeneralLedgerQueryDto query,
            CancellationToken cancellationToken);
    }
}