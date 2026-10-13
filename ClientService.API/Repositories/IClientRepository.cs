using ClientService.API.Models;

namespace ClientService.API.Repositories
{
    public interface IClientRepository
    {
        Task<(List<Client> Data, int Total)> GetAllAsync(
            int page,
            int pageSize,
            string? search,
            CancellationToken cancellationToken);

        Task<Client?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<Client> CreateAsync(
            Client client,
            CancellationToken cancellationToken);

        Task<Client> UpdateAsync(
            Client client,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}