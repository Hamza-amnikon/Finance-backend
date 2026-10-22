using BankingService.API.DTOs;

namespace BankingService.API.Services
{
    public interface IBankTransactionService
    {
        Task<BankTransactionListDto> GetPagedAsync(
            BankTransactionQueryDto query,
            CancellationToken cancellationToken);

        Task<BankTransactionResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankTransactionResponseDto> CreateAsync(
            CreateBankTransactionDto dto,
            CancellationToken cancellationToken);

        Task<BankTransactionResponseDto?> UpdateAsync(
            int id,
            UpdateBankTransactionDto dto,
            CancellationToken cancellationToken);

        Task<BankTransactionResponseDto?> PostAsync(
            int id,
            BankTransactionVersionDto dto,
            CancellationToken cancellationToken);

        Task<BankTransactionResponseDto?> CancelAsync(
            int id,
            BankTransactionVersionDto dto,
            CancellationToken cancellationToken);
    }
}