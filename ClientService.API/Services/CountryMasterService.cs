using ClientService.API.Repositories;

namespace ClientService.API.Services
{
    public class CountryMasterService : ICountryMasterService
    {
        private readonly ICountryMasterRepository _repository;

        public CountryMasterService(
            ICountryMasterRepository repository)
        {
            _repository = repository;
        }

        public Task<List<string>> GetCountriesAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetCountriesAsync(
                cancellationToken);
        }

        public Task<List<string>> GetStatesAsync(
            string country,
            CancellationToken cancellationToken)
        {
            return _repository.GetStatesAsync(
                country.Trim(),
                cancellationToken);
        }

        public Task<List<string>> GetCitiesAsync(
            string country,
            string? state,
            CancellationToken cancellationToken)
        {
            return _repository.GetCitiesAsync(
                country.Trim(),
                state?.Trim(),
                cancellationToken);
        }
    }
}