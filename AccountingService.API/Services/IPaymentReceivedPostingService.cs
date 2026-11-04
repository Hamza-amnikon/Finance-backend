using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IPaymentReceivedPostingService
    {
        Task<PaymentReceivedPostingResponseDto> PostAsync(
            PostPaymentReceivedDto dto,
            CancellationToken cancellationToken);
    }
}
