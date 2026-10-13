namespace ClientService.API.Services
{
    public interface ICountryMasterService
    {
        Task<List<string>> GetCountriesAsync(
            CancellationToken cancellationToken);

        Task<List<string>> GetStatesAsync(
            string country,
            CancellationToken cancellationToken);

        Task<List<string>> GetCitiesAsync(
            string country,
            string? state,
            CancellationToken cancellationToken);
    }
}