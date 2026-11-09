using ExchangeRateService.API.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ExchangeRateService.API.Services
{
    public class ExchangeRateProvider : IDailyRateProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ExchangeRateProviderOptions _options;
        private readonly TimeProvider _clock;

        public ExchangeRateProvider(
            HttpClient httpClient,
            IMemoryCache cache,
            IOptions<ExchangeRateProviderOptions> options,
            TimeProvider clock)
        {
            _httpClient = httpClient;
            _cache = cache;
            _options = options.Value;
            _clock = clock;
        }

        public async Task<ExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly? date,
            CancellationToken cancellationToken)
        {
            baseCurrency = NormalizeCurrency(baseCurrency);
            quoteCurrency = NormalizeCurrency(quoteCurrency);

            var now = _clock.GetUtcNow();
            var rateDate = date ?? DateOnly.FromDateTime(now.UtcDateTime);

            if (baseCurrency == quoteCurrency)
            {
                return new ExchangeRateDto
                {
                    Rate = 1m,
                    RateDate = rateDate,
                    Source = "Same currency",
                    RetrievedAtUtc = now
                };
            }

            var key = (
                _options.BaseUrl,
                _options.ProviderName,
                baseCurrency,
                quoteCurrency,
                date);

            if (_cache.TryGetValue<ExchangeRateDto>(key, out var cached))
            {
                return cached!;
            }

            var path = "rate/" +
                Uri.EscapeDataString(baseCurrency) + "/" +
                Uri.EscapeDataString(quoteCurrency);

            if (date.HasValue)
            {
                path += "?date=" + date.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }

            using var response = await _httpClient.GetAsync(
                path,
                cancellationToken);

            if (
                response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new HttpRequestException(
                    "No reference rate is available for these currencies and this date.",
                    null,
                    response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "The exchange-rate provider is unavailable. Retry shortly.",
                    null,
                    response.StatusCode);
            }

            ProviderRate? providerRate;

            try
            {
                providerRate = await response.Content.ReadFromJsonAsync<ProviderRate>(
                    cancellationToken);
            }
            catch (Exception exception)
                when (exception is JsonException || exception is NotSupportedException)
            {
                throw new HttpRequestException(
                    "The exchange-rate provider returned an invalid response.",
                    exception);
            }

            if (
                providerRate == null ||
                providerRate.Rate <= 0 ||
                providerRate.Date == default ||
                providerRate.Date > rateDate ||
                !string.Equals(providerRate.Base, baseCurrency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(providerRate.Quote, quoteCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException(
                    "The exchange-rate provider returned an invalid currency pair, rate or date.");
            }

            var result = new ExchangeRateDto
            {
                Rate = providerRate.Rate,
                RateDate = providerRate.Date,
                Source = _options.ProviderName,
                RetrievedAtUtc = now
            };

            _cache.Set(
                key,
                result,
                TimeSpan.FromMinutes(_options.CacheMinutes));

            return result;
        }

        private static string NormalizeCurrency(string currency)
        {
            var normalized = currency?.Trim().ToUpperInvariant() ?? "";

            if (
                normalized.Length != 3 ||
                normalized.Any(character => character < 'A' || character > 'Z'))
            {
                throw new ValidationException(
                    "Currency codes must contain three letters.");
            }

            return normalized;
        }

        private sealed class ProviderRate
        {
            public string Base { get; set; } = "";

            public string Quote { get; set; } = "";

            public decimal Rate { get; set; }

            public DateOnly Date { get; set; }
        }
    }
}
