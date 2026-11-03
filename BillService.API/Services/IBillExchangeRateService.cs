namespace BillService.API.Services
{
    public interface IBillExchangeRateService
    {
        Task<decimal> GetRateAsync(
            string fromCurrency,
            string toCurrency,
            DateOnly rateDate,
            CancellationToken cancellationToken);
    }
}
