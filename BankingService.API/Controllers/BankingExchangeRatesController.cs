using System.ComponentModel.DataAnnotations;
using BankingService.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/bank-transfers/exchange-rate")]
    public class BankingExchangeRatesController : ControllerBase
    {
        private readonly IBankingExchangeRateService _service;
        private readonly ILogger<BankingExchangeRatesController> _logger;

        public BankingExchangeRatesController(
            IBankingExchangeRateService service,
            ILogger<BankingExchangeRatesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetRate(
            [FromQuery, Required, RegularExpression("^[A-Za-z]{3}$")] string from,
            [FromQuery, Required, RegularExpression("^[A-Za-z]{3}$")] string to,
            [FromQuery] DateOnly? date,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetRateAsync(
                    from,
                    to,
                    date,
                    cancellationToken);

                return Ok(result);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(504, new
                {
                    message = "The rate lookup timed out. Retry shortly."
                });
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Banking reference-rate lookup failed.");

                return StatusCode(503, new
                {
                    message = "The reference rate is unavailable. Retry shortly or record the actual bank receipt without an estimate."
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to load the banking reference rate.");

                return StatusCode(503, new
                {
                    message = "Unable to load the reference rate. Retry shortly."
                });
            }
        }
    }
}
