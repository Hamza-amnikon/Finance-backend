using BankingService.API.DTOs;

namespace BankingService.API.Services
{
    public interface IBankReconciliationService
    {
        Task<BankReconciliationListDto> GetPagedAsync(
            BankReconciliationQueryDto query,
            CancellationToken cancellationToken);

        Task<BankReconciliationResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankReconciliationResponseDto> CreateAsync(
            CreateBankReconciliationDto dto,
            CancellationToken cancellationToken);

        Task<BankReconciliationResponseDto?> MatchAsync(
            int id,
            int lineId,
            MatchBankStatementLineDto dto,
            CancellationToken cancellationToken);

        Task<BankReconciliationResponseDto?> CompleteAsync(
            int id,
            BankReconciliationVersionDto dto,
            CancellationToken cancellationToken);
    }
}