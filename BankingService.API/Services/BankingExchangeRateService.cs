using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BankingService.API.DTOs;
using BankingService.API.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BankingService.API.Services
{
    public class BankingExchangeRateService : IBankingExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly IBankAccountRepository _accounts;
        private readonly IMemoryCache _cache;
        private readonly BankingExchangeRateOptions _options;
        private readonly TimeProvider _clock;

        public BankingExchangeRateService(
            HttpClient httpClient,
            IBankAccountRepository accounts,
            IMemoryCache cache,
            IOptions<BankingExchangeRateOptions> options,
            TimeProvider clock)
        {
            _httpClient = httpClient;
            _accounts = accounts;
            _cache = cache;
            _options = options.Value;
            _clock = clock;
        }

        public async Task<BankingExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly? date,
            CancellationToken cancellationToken)
        {
            baseCurrency = NormalizeCurrency(baseCurrency);
            quoteCurrency = NormalizeCurrency(quoteCurrency);

            if (date == default(DateOnly))
            {
                throw new ValidationException("Select a valid exchange-rate date.");
            }

            var sourceCurrency = await _accounts.GetCurrencyAsync(
                baseCurrency,
                cancellationToken);

            var destinationCurrency = await _accounts.GetCurrencyAsync(
                quoteCurrency,
                cancellationToken);

            if (sourceCurrency == null || destinationCurrency == null)
            {
                throw new ValidationException(
                    "Select currencies configured in Finance.");
            }

            var now = _clock.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);

            if (baseCurrency == quoteCurrency)
            {
                return new BankingExchangeRateDto
                {
                    BaseCurrency = baseCurrency,
                    QuoteCurrency = quoteCurrency,
                    Rate = 1m,
                    RateText = "1",
                    RateDate = date ?? today,
                    Source = "Same currency",
                    RetrievedAtUtc = now
                };
            }

            var key = (
                "banking-reference-rate",
                _options.BaseUrl,
                _options.ProviderName,
                baseCurrency,
                quoteCurrency,
                date);

            if (_cache.TryGetValue<BankingExchangeRateDto>(key, out var cached))
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
                    "No reference rate is available for these currencies and this date.");
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
            catch (Exception exception)
                when (exception is JsonException || exception is NotSupportedException)
            {
                throw new HttpRequestException(
                    "The exchange-rate provider returned an invalid response.",
                    exception);
            }

            if (
                rate == null ||
                rate.Rate <= 0 ||
                rate.Date == default ||
                rate.Date > (date ?? today.AddDays(1)) ||
                !string.Equals(rate.Base, baseCurrency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rate.Quote, quoteCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException(
                    "The exchange-rate provider returned an invalid currency pair, rate or date.");
            }

            var result = new BankingExchangeRateDto
            {
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                Rate = rate.Rate,
                RateText = rate.Rate.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture),
                RateDate = rate.Date,
                Source = _options.ProviderName,
                RetrievedAtUtc = now
            };

            _cache.Set(
                key,
                result,
                TimeSpan.FromMinutes(_options.CacheMinutes));

            return result;
        }

        private static string NormalizeCurrency(
            string currency)
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

        private class ProviderRate
        {
            public string Base { get; set; } = "";

            public string Quote { get; set; } = "";

            public DateOnly Date { get; set; }

            public decimal Rate { get; set; }
        }
    }
}
