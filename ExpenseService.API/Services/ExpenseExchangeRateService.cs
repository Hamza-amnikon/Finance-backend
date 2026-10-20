using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseService.API.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ExpenseService.API.Services
{
    public class ExpenseExchangeRateService : IExpenseExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly IExpenseLookupService _lookups;
        private readonly IMemoryCache _cache;
        private readonly ExchangeRateOptions _options;
        private readonly TimeProvider _clock;

        public ExpenseExchangeRateService(
            HttpClient httpClient,
            IExpenseLookupService lookups,
            IMemoryCache cache,
            IOptions<ExchangeRateOptions> options,
            TimeProvider clock)
        {
            _httpClient = httpClient;
            _lookups = lookups;
            _cache = cache;
            _options = options.Value;
            _clock = clock;
        }

        public async Task<ExpenseExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly date,
            CancellationToken cancellationToken)
        {
            if (date == default || date > DateOnly.FromDateTime(_clock.GetLocalNow().DateTime))
            {
                throw new ValidationException(
                    "Select an expense date on or before today for an automatic rate.");
            }

            await _lookups.ResolveCurrencyAsync(
                baseCurrency,
                false,
                cancellationToken);

            await _lookups.ResolveCurrencyAsync(
                quoteCurrency,
                false,
                cancellationToken);

            if (baseCurrency == quoteCurrency)
            {
                return new ExpenseExchangeRateDto
                {
                    RateBaseCurrency = baseCurrency,
                    RateQuoteCurrency = quoteCurrency,
                    Rate = 1m,
                    RateDate = date,
                    Source = "Same currency"
                };
            }

            var key = ("expense-rate", _options.BaseUrl, baseCurrency, quoteCurrency, date);

            if (_cache.TryGetValue<ExpenseExchangeRateDto>(key, out var cached))
            {
                return cached!;
            }

            var path = "rate/" +
                Uri.EscapeDataString(baseCurrency) + "/" +
                Uri.EscapeDataString(quoteCurrency) +
                "?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            using var response = await _httpClient.GetAsync(
                path,
                cancellationToken);

            if (
                response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new HttpRequestException(
                    "No exchange rate is available for these currencies and this date.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "The exchange-rate provider is unavailable. Retry shortly.");
            }

            ProviderRate? rate;

            try
            {
                rate = await response.Content.ReadFromJsonAsync<ProviderRate>(
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException(
                    "The exchange-rate provider returned an invalid response.",
                    exception);
            }

            if (
                rate == null ||
                rate.Date == default ||
                rate.Date > date ||
                !string.Equals(rate.Base, baseCurrency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rate.Quote, quoteCurrency, StringComparison.OrdinalIgnoreCase) ||
                rate.Rate <= 0 ||
                rate.Rate > 1000000m)
            {
                throw new HttpRequestException(
                    "The exchange-rate provider returned an invalid rate or date.");
            }

            var rounded = decimal.Round(
                rate.Rate,
                6,
                MidpointRounding.AwayFromZero);

            if (rounded <= 0)
            {
                throw new HttpRequestException(
                    "This exchange rate cannot be represented with the supported precision.");
            }

            var result = new ExpenseExchangeRateDto
            {
                RateBaseCurrency = baseCurrency,
                RateQuoteCurrency = quoteCurrency,
                Rate = rounded,
                RateDate = rate.Date,
                Source = _options.ProviderName
            };

            _cache.Set(
                key,
                result,
                TimeSpan.FromMinutes(_options.CacheMinutes));

            return result;
        }

        private class ProviderRate
        {
            public DateOnly Date { get; set; }

            [JsonPropertyName("base")]
            public string Base { get; set; } = "";

            public string Quote { get; set; } = "";

            public decimal Rate { get; set; }
        }
    }
}
