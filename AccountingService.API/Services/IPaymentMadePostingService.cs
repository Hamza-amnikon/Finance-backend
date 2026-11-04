using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public interface IPaymentMadePostingService
    {
        Task<PaymentMadePostingResponseDto> PostAsync(
            PostPaymentMadeDto dto,
            CancellationToken cancellationToken);
    }
}
