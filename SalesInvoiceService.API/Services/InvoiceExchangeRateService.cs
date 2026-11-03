using System.Net.Http.Json;
using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public class InvoiceExchangeRateService
        : IInvoiceExchangeRateService
    {
        private readonly HttpClient _httpClient;

        public InvoiceExchangeRateService(
            HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<decimal> GetRateAsync(
            string fromCurrency,
            string toCurrency,
            DateOnly rateDate,
            CancellationToken cancellationToken)
        {
            var from =
                NormalizeCurrency(
                    fromCurrency);

            var to =
                NormalizeCurrency(
                    toCurrency);

            if (from == to)
            {
                return 1m;
            }

            using var response =
                await _httpClient.GetAsync(
                    $"api/exchange-rates/rate/{from}/{to}" +
                    $"?date={rateDate:yyyy-MM-dd}",
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "A saved exchange rate is unavailable for this invoice date.",
                    null,
                    response.StatusCode);
            }

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        SalesInvoiceExchangeRateDto>(
                        cancellationToken:
                            cancellationToken)
                ?? throw new HttpRequestException(
                    "ExchangeRateService returned an empty response.");

            if (result.Rate <= 0)
            {
                throw new HttpRequestException(
                    "ExchangeRateService returned an invalid rate.");
            }

            return decimal.Round(
                result.Rate,
                8,
                MidpointRounding.AwayFromZero);
        }

        private static string NormalizeCurrency(
            string currency)
        {
            var normalized =
                currency.Trim().ToUpperInvariant();

            if (
                normalized.Length != 3 ||
                !normalized.All(char.IsLetter))
            {
                throw new ArgumentException(
                    "Currency must be a valid three-letter code.");
            }

            return normalized;
        }
    }
}
