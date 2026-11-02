using ExchangeRateService.API.DTOs;
using System.ComponentModel.DataAnnotations;
using ExchangeRateService.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExchangeRateService.API.Services
{
    public class StoredExchangeRateService : IExchangeRateService
    {
        private readonly ExchangeRateDbContext _context;
        private readonly IDailyRateProvider _provider;
        private readonly IOptions<DailyRateOptions> _options;
        private readonly TimeProvider _timeProvider;

        public StoredExchangeRateService(
            ExchangeRateDbContext context,
            IDailyRateProvider provider,
            IOptions<DailyRateOptions> options,
            TimeProvider timeProvider)
        {
            _context = context;
            _provider = provider;
            _options = options;
            _timeProvider = timeProvider;
        }

        public async Task<ExchangeRateDto> GetRateAsync(
            string baseCurrency,
            string quoteCurrency,
            DateOnly? date,
            CancellationToken cancellationToken)
        {
            baseCurrency = Normalize(baseCurrency);
            quoteCurrency = Normalize(quoteCurrency);
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            if (date > today)
            {
                throw new ValidationException(
                    "Exchange-rate dates cannot be in the future.");
            }

            if (baseCurrency == quoteCurrency)
            {
                return new ExchangeRateDto
                {
                    Rate = 1,
                    RateDate = date ?? today,
                    Source = "Same currency",
                    RetrievedAtUtc = _timeProvider.GetUtcNow()
                };
            }

            var cutoff = date ?? today;
            var rows = await _context.DailyExchangeRates.AsNoTracking()
                .Where(row => (row.Currency == baseCurrency || row.Currency == quoteCurrency) && row.RateDate <= cutoff)
                .OrderByDescending(row => row.CheckDate).ToListAsync(cancellationToken);
            foreach (var batch in rows.GroupBy(row => row.CheckDate))
            {
                var from = batch.FirstOrDefault(row => row.Currency == baseCurrency);
                var to = batch.FirstOrDefault(row => row.Currency == quoteCurrency);
                if (from == null || to == null ||
                    (baseCurrency != "INR" && quoteCurrency != "INR" && from.RateDate != to.RateDate))
                {
                    continue;
                }
                if (cutoff == today && batch.Key < _options.Value.ScheduledDate(_timeProvider.GetUtcNow()))
                {
                    break;
                }
                return new ExchangeRateDto
                {
                    Rate = from.RateToInr / to.RateToInr,
                    RateDate = baseCurrency == "INR" ? to.RateDate : from.RateDate,
                    Source = from.Source,
                    RetrievedAtUtc = from.RetrievedAtUtc > to.RetrievedAtUtc
                        ? from.RetrievedAtUtc
                        : to.RetrievedAtUtc
                };
            }

            // Historical dates before monitoring began remain available through the shared provider adapter.
            if (date.HasValue && date.Value < today)
            {
                return await _provider.GetRateAsync(
                    baseCurrency,
                    quoteCurrency,
                    date,
                    cancellationToken);
            }

            throw new HttpRequestException("The daily agent has no current saved rate for this pair. Check Exchange Rates status.");
        }

        private static string Normalize(string value)
        {
            var code = value?.Trim().ToUpperInvariant() ?? "";
            if (code.Length != 3 || code.Any(letter => letter < 'A' || letter > 'Z'))
            {
                throw new ValidationException("Use three-letter currency codes.");
            }
            return code;
        }
    }
}
