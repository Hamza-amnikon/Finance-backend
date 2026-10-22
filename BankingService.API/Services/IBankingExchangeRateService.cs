using BankingService.API.DTOs;

namespace BankingService.API.Services
{
    public interface IBankingExchangeRateService
    {
        Task<BankingExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly? date,
            CancellationToken cancellationToken);
    }
}
