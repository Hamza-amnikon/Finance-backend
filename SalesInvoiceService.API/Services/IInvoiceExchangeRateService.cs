namespace SalesInvoiceService.API.Services
{
    public interface IInvoiceExchangeRateService
    {
        Task<decimal> GetRateAsync(
            string fromCurrency,
            string toCurrency,
            DateOnly rateDate,
            CancellationToken cancellationToken);
    }
}
