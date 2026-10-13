using System.ComponentModel.DataAnnotations;
using ClientService.API.Services;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetCountries(
            CancellationToken cancellationToken)
        {
            var countries = await _service.GetCountriesAsync(
                cancellationToken);

            return Ok(countries);
        }

        [HttpGet("states")]
        public async Task<IActionResult> GetStates(
            [FromQuery, Required, MaxLength(100)] string country,
            CancellationToken cancellationToken)
        {
            var states = await _service.GetStatesAsync(
                country,
                cancellationToken);

            return Ok(states);
        }

        [HttpGet("cities")]
        public async Task<IActionResult> GetCities(
            [FromQuery, Required, MaxLength(100)] string country,
            [FromQuery, MaxLength(100)] string? state,
            CancellationToken cancellationToken)
        {
            var cities = await _service.GetCitiesAsync(
                country,
                state,
                cancellationToken);

            return Ok(cities);
        }
    }
}