using ClientService.API.DTOs;

namespace ClientService.API.Services
{
    public interface IClientService
    {
        Task<(List<ClientResponseDto> Data, int Total)> GetAllAsync(
            ClientQueryDto query,
            CancellationToken cancellationToken);

        Task<ClientResponseDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<ClientResponseDto> CreateAsync(
            CreateClientDto dto,
            CancellationToken cancellationToken);

        Task<ClientResponseDto?> UpdateAsync(
            Guid id,
            UpdateClientDto dto,
            CancellationToken cancellationToken);

        Task<bool> ArchiveAsync(
            Guid id,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}