using System.ComponentModel.DataAnnotations;
using ExpenseService.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseService.API.Controllers
{
    [ApiController]
    [Route("api/expenses/exchange-rate")]
    public class ExpenseExchangeRatesController : ControllerBase
    {
        private readonly IExpenseExchangeRateService _service;
        private readonly ILogger<ExpenseExchangeRatesController> _logger;

        public ExpenseExchangeRatesController(
            IExpenseExchangeRateService service,
            ILogger<ExpenseExchangeRatesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetRate(
            [FromQuery, Required, RegularExpression("^[A-Z]{3}$")] string from,
            [FromQuery, Required, RegularExpression("^[A-Z]{3}$")] string to,
            [FromQuery] DateOnly date,
            CancellationToken cancellationToken)
        {
            try
            {
                var rate = await _service.GetRateAsync(
                    from,
                    to,
                    date,
                    cancellationToken);

                return Ok(rate);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(504, new
                {
                    message = "Exchange-rate lookup timed out. Retry shortly."
                });
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Exchange-rate lookup failed.");

                return StatusCode(503, new
                {
                    message = "Unable to load an automatic exchange rate. Retry shortly."
                });
            }
        }
    }
}
