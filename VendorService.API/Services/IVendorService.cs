using VendorService.API.DTOs;

namespace VendorService.API.Services
{
    public interface IVendorService
    {
        Task<(List<VendorResponseDto> Vendors, int Total)> GetPagedAsync(
            int page,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken);

        Task<VendorResponseDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<VendorResponseDto> CreateAsync(
            CreateVendorDto dto,
            CancellationToken cancellationToken);

        Task<VendorResponseDto?> UpdateAsync(
            Guid id,
            UpdateVendorDto dto,
            CancellationToken cancellationToken);

        Task<bool> ArchiveAsync(
            Guid id,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}