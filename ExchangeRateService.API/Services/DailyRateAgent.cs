using System.ComponentModel.DataAnnotations;
using ExchangeRateService.API.Data;
using ExchangeRateService.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExchangeRateService.API.Services
{
    public class DailyRateAgent
    {
        private readonly ExchangeRateDbContext _context;
        private readonly IDailyRateProvider _provider;
        private readonly IOptions<DailyRateOptions> _options;
        private readonly TimeProvider _timeProvider;
        public DailyRateAgent(
            ExchangeRateDbContext context,
            IDailyRateProvider provider,
            IOptions<DailyRateOptions> options,
            TimeProvider timeProvider
        )
        {
            _context = context;
            _provider = provider;
            _options = options;
            _timeProvider = timeProvider;
        }

        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            if (!_options.Value.Enabled)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var day = _options.Value.ScheduledDate(now);
            await _context.Database.OpenConnectionAsync(cancellationToken);
            var locked = false;
            try
            {
                // Serialize workers across API instances without holding a transaction during HTTP calls.
                var result = await _context.Database.SqlQueryRaw<int>("""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource=N'FinanceDailyExchangeRates',
                        @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
                    SELECT @result AS Value;
                    """).ToListAsync(cancellationToken);
                if (result[0] < 0)
                {
                    return;
                }

                locked = true;
                var check = await _context.ExchangeRateChecks.FindAsync([day], cancellationToken);
                if (check?.Status == "Succeeded")
                {
                    return;
                }

                if (check != null && now - check.CheckedAtUtc < TimeSpan.FromMinutes(_options.Value.RetryMinutes))
                {
                    return;
                }

                check ??= new ExchangeRateCheck
                {
                    CheckDate = day
                };
                if (_context.Entry(check).State == EntityState.Detached)
                {
                    _context.Add(check);
                }

                check.Status = "Running";
                check.CheckedAtUtc = now;
                check.Error = null;
                await _context.SaveChangesAsync(cancellationToken);
                var currencies = await _context.Database.SqlQueryRaw<string>("SELECT CurrencyCode AS Value FROM sales.CurrencyMasters WHERE IsActive = 1").ToListAsync(cancellationToken);
                currencies.Add("INR");
                var errors = new List<string>();
                foreach (var currency in currencies.Select(value => value.Trim().ToUpperInvariant()).Distinct())
                {
                    try
                    {
                        var rate = await _provider.GetRateAsync(currency, "INR", day, cancellationToken);
                        if (rate.Rate <= 0 || rate.Rate > 1000000m || rate.RateDate == default || rate.RateDate > day)
                        {
                            throw new ValidationException($"Provider returned an invalid rate or a publication date after {day:yyyy-MM-dd}.");
                        }

                        var saved = await _context.DailyExchangeRates.SingleOrDefaultAsync(row => row.CheckDate == day && row.Currency == currency, cancellationToken);
                        saved ??= new DailyExchangeRate
                        {
                            CheckDate = day,
                            Currency = currency
                        };
                        if (_context.Entry(saved).State == EntityState.Detached)
                        {
                            _context.Add(saved);
                        }

                        saved.RateToInr = rate.Rate;
                        saved.RateDate = rate.RateDate;
                        saved.Source = rate.Source;
                        saved.RetrievedAtUtc = _timeProvider.GetUtcNow();
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception exception) when (exception is HttpRequestException or ValidationException or OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var reason = exception switch
                        {
                            ValidationException => exception.Message,
                            OperationCanceledException => "Provider request timed out.",
                            HttpRequestException http when http.StatusCode.HasValue =>
                                $"Provider returned HTTP {(int)http.StatusCode.Value}.",
                            _ => "Provider request failed or returned an invalid response."
                        };
                        errors.Add($"{currency}: {reason}");
                    }
                }

                check.CheckedAtUtc = _timeProvider.GetUtcNow();
                check.Status = errors.Count == 0 ? "Succeeded" : "Partial";
                var errorText = string.Join("; ", errors);
                check.Error = errors.Count == 0 ? null : errorText[..Math.Min(1000, errorText.Length)];
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Record an interrupted check when SQL is still available; never discard saved rates.
                try
                {
                    _context.ChangeTracker.Clear();
                    var failed = await _context.ExchangeRateChecks.FindAsync([day], cancellationToken);
                    if (failed != null)
                    {
                        failed.Status = "Failed";
                        failed.CheckedAtUtc = _timeProvider.GetUtcNow();
                        failed.Error = "The daily check could not finish. A retry is scheduled.";
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                { /* The worker logs the original failure. */
                }

                throw;
            }
            finally
            {
                if (locked)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                                        "EXEC sys.sp_releaseapplock @Resource=N'FinanceDailyExchangeRates', @LockOwner='Session'",
                                        CancellationToken.None
                                    );
                }

                await _context.Database.CloseConnectionAsync();
            }
        }
    }
}
