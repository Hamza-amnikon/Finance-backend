using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> GetActive(
            CancellationToken cancellationToken)
        {
            var currencies = await _service.GetActiveAsync(
                cancellationToken);

            return Ok(currencies);
        }

        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode(
            string code,
            CancellationToken cancellationToken)
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
        }
    }
}