using SparkFinance.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesInvoiceService.API.Services;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/currencies")]
    public class CurrenciesController : ControllerBase
    {
        private readonly ICurrencyService _service;

        public CurrenciesController(ICurrencyService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> GetActive(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var currencies = await _service.GetActiveAsync(
                    cancellationToken);

                return Ok(currencies);
            }, cancellationToken);
        }

        [HttpGet("{code}")]
        public Task<IActionResult> GetByCode(
            string code,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(async () =>
            {
                var currency = await _service.GetByCodeAsync(
                    code,
                    cancellationToken);

                if (currency == null)
                {
                    return NotFound(new
                    {
                        message = "Active currency not found."
                    });
                }

                return Ok(currency);
            }, cancellationToken);
        }

        private async Task<IActionResult> HandleReadAsync(
            Func<Task<IActionResult>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                return await action();
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
        }
    }
}