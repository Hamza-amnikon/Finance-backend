using System.ComponentModel.DataAnnotations;
using ExchangeRateService.API.Data;
using ExchangeRateService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExchangeRateService.API.Controllers
{
    [ApiController]
    [Route("api/exchange-rates")]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly ExchangeRateDbContext _context;
        private readonly IExchangeRateService _rateService;
        private readonly IOptions<DailyRateOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ExchangeRatesController> _logger;

        public ExchangeRatesController(
            ExchangeRateDbContext context,
            IExchangeRateService rateService,
            IOptions<DailyRateOptions> options,
            TimeProvider timeProvider,
            ILogger<ExchangeRatesController> logger)
        {
            _context = context;
            _rateService = rateService;
            _options = options;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        [HttpGet("latest")]
        public async Task<IActionResult> Latest(
            CancellationToken cancellationToken)
        {
            try
            {
                var check = await _context.ExchangeRateChecks
                    .AsNoTracking()
                    .OrderByDescending(row => row.CheckDate)
                    .FirstOrDefaultAsync(cancellationToken);

                var saved = await _context.DailyExchangeRates
                    .AsNoTracking()
                    .Where(row => _context.DailyExchangeRates
                        .Where(other => other.Currency == row.Currency)
                        .Max(other => other.CheckDate) == row.CheckDate)
                    .OrderBy(row => row.Currency)
                    .ToListAsync(cancellationToken);

                var currencies = await _context.Database
                    .SqlQueryRaw<string>(
                        "SELECT CurrencyCode AS Value FROM sales.CurrencyMasters WHERE IsActive = 1")
                    .ToListAsync(cancellationToken);

                currencies.Add("INR");

                var now = _timeProvider.GetUtcNow();
                var scheduled = _options.Value.ScheduledDate(now);
                var status = "Waiting";

                if (check != null)
                {
                    status = check.CheckDate < scheduled
                        ? "Overdue"
                        : check.Status;
                }

                var nextCheck = check != null && check.Status != "Succeeded"
                    ? check.CheckedAtUtc.AddMinutes(_options.Value.RetryMinutes)
                    : _options.Value.NextCheck(now);

                var rates = currencies
                    .Select(code => code.Trim().ToUpperInvariant())
                    .Distinct()
                    .Order()
                    .Select(code =>
                    {
                        var row = saved.FirstOrDefault(
                            value => value.Currency == code);

                        return new
                        {
                            currency = code,
                            quoteCurrency = "INR",
                            rateToInr = (decimal?)row?.RateToInr,
                            rateDate = (DateOnly?)row?.RateDate,
                            source = row?.Source,
                            lastSuccessfulCheckAtUtc = row?.RetrievedAtUtc,
                            isCurrentCheck = row != null && row.CheckDate == scheduled
                        };
                    });

                return Ok(new
                {
                    enabled = _options.Value.Enabled,
                    status,
                    lastCheckedAtUtc = check?.CheckedAtUtc,
                    nextCheckAtUtc = nextCheck,
                    warning = check?.Error,
                    rates
                });
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Could not read daily exchange-rate status.");

                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Exchange-rate storage is unavailable. Check the Exchange Rate API setup.");
            }
        }

        // Keep the response compatible with Banking and Expenses.
        [HttpGet("rate/{from}/{to}")]
        public async Task<IActionResult> Pair(
            string from,
            string to,
            [FromQuery] DateOnly? date,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _rateService.GetRateAsync(
                    from,
                    to,
                    date,
                    cancellationToken);

                return Ok(new
                {
                    @base = from.Trim().ToUpperInvariant(),
                    quote = to.Trim().ToUpperInvariant(),
                    date = result.RateDate,
                    rate = result.Rate,
                    source = result.Source
                });
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Saved exchange-rate lookup failed.");

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message = "A current reference rate is unavailable. Check the Exchange Rates page."
                    });
            }
        }
    }
}
