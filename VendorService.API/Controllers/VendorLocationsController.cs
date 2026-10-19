using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using VendorService.API.Services;

namespace VendorService.API.Controllers
{
    [ApiController]
    [Route("api/vendors/locations")]
    public class VendorLocationsController : ControllerBase
    {
        private readonly ICountryMasterService _service;
        private readonly ILogger<VendorLocationsController> _logger;

        public VendorLocationsController(
            ICountryMasterService service,
            ILogger<VendorLocationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("countries")]
        public Task<IActionResult> GetCountries(CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.GetCountriesAsync(cancellationToken));
        }

        [HttpGet("currencies")]
        public Task<IActionResult> GetCurrencies(
            [FromQuery, Required, MaxLength(100)] string country,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.GetCurrencyCodesAsync(country, cancellationToken));
        }

        [HttpGet("states")]
        public Task<IActionResult> GetStates(
            [FromQuery, Required, MaxLength(100)] string country,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.GetStatesAsync(country, cancellationToken));
        }

        [HttpGet("cities")]
        public Task<IActionResult> GetCities(
            [FromQuery, Required, MaxLength(100)] string country,
            [FromQuery, MaxLength(100)] string? state,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.GetCitiesAsync(country, state, cancellationToken));
        }

        private async Task<IActionResult> ExecuteAsync(Func<Task<List<string>>> load)
        {
            try
            {
                return Ok(await load());
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Unable to read the shared country master table.");
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Location data is temporarily unavailable.");
            }
        }
    }
}
