namespace ClientService.API.Repositories
{
    public interface ICountryMasterRepository
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