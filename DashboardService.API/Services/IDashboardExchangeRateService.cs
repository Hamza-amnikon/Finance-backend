namespace DashboardService.API.Services
{
    public interface IDashboardExchangeRateService
    {
        Task<DashboardExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly? date,
            CancellationToken cancellationToken);
    }
}
