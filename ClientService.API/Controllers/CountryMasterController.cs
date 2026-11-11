using System.ComponentModel.DataAnnotations;
using ClientService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ClientService.API.Controllers
{
    [ApiController]
    [Route("api/country-master")]
    public class CountryMasterController : ControllerBase
    {
        private readonly ICountryMasterService _service;

        public CountryMasterController(
            ICountryMasterService service)
        {
            _service = service;
        }

        [HttpGet("countries")]
        public Task<IActionResult> GetCountries(
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(
                () => _service.GetCountriesAsync(cancellationToken),
                cancellationToken);
        }

        [HttpGet("states")]
        public Task<IActionResult> GetStates(
            [FromQuery, Required, MaxLength(100)] string country,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(
                () => _service.GetStatesAsync(country, cancellationToken),
                cancellationToken);
        }

        [HttpGet("cities")]
        public Task<IActionResult> GetCities(
            [FromQuery, Required, MaxLength(100)] string country,
            [FromQuery, MaxLength(100)] string? state,
            CancellationToken cancellationToken)
        {
            return HandleReadAsync(
                () => _service.GetCitiesAsync(country, state, cancellationToken),
                cancellationToken);
        }

        private async Task<IActionResult> HandleReadAsync(
            Func<Task<List<string>>> load,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await load());
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
                when (cancellationToken.IsCancellationRequested && exception.Number == 0)
            {
                return StatusCode(499);
            }
        }
    }
}
