using BankingService.API.DTOs;

namespace BankingService.API.Services
{
    public interface IBankTransferService
    {
        Task<BankTransferListDto> GetPagedAsync(
            BankTransferQueryDto query,
            CancellationToken cancellationToken);

        Task<BankTransferResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<BankTransferResponseDto> CreateAsync(
            CreateBankTransferDto dto,
            CancellationToken cancellationToken);

        Task<BankTransferResponseDto?> UpdateAsync(
            int id,
            UpdateBankTransferDto dto,
            CancellationToken cancellationToken);

        Task<BankTransferResponseDto?> ConfirmSentAsync(
            int id,
            ConfirmBankTransferSentDto dto,
            CancellationToken cancellationToken);

        Task<BankTransferResponseDto?> ConfirmReceivedAsync(
            int id,
            ConfirmBankTransferReceivedDto dto,
            CancellationToken cancellationToken);

        Task<BankTransferResponseDto?> CancelAsync(
            int id,
            BankTransferVersionDto dto,
            CancellationToken cancellationToken);
    }
}