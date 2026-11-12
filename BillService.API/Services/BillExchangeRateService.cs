using System.Net.Http.Json;

namespace BillService.API.Services
{
    public class BillExchangeRateService : IBillExchangeRateService
    {
        private readonly HttpClient _httpClient;

        public BillExchangeRateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<decimal> GetRateAsync(
            string fromCurrency,
            string toCurrency,
            DateOnly rateDate,
            CancellationToken cancellationToken)
        {
            var from = fromCurrency.Trim().ToUpperInvariant();
            var to = toCurrency.Trim().ToUpperInvariant();

            if (from == to)
            {
                return 1m;
            }

            using var response = await _httpClient.GetAsync(
                $"api/exchange-rates/rate/{from}/{to}?date={rateDate:yyyy-MM-dd}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "A saved exchange rate is unavailable for this bill date.",
                    null,
                    response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<BillExchangeRateDto>(
                cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("ExchangeRateService returned an empty response.");

            if (result.Rate <= 0)
            {
                throw new HttpRequestException("ExchangeRateService returned an invalid rate.");
            }

            return decimal.Round(result.Rate, 8, MidpointRounding.AwayFromZero);
        }

        private sealed class BillExchangeRateDto
        {
            public decimal Rate { get; set; }
        }
    }
}
