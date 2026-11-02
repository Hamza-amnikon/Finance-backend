using ExchangeRateService.API.DTOs;
namespace ExchangeRateService.API.Services
{
    public interface IExchangeRateService
    {
        Task<ExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly? date,
            CancellationToken cancellationToken);
    }
}
