using BankingService.API.DTOs;

namespace BankingService.API.Services
{
    public interface IPaymentAccountingApiService
    {
        Task<PaymentReceivedAccountingResultDto>
            PostPaymentReceivedAsync(
                PaymentReceivedAccountingPostingDto posting,
                CancellationToken cancellationToken);

        Task<PaymentMadeAccountingResultDto>
            PostPaymentMadeAsync(
                PaymentMadeAccountingPostingDto posting,
                CancellationToken cancellationToken);
    }
}
