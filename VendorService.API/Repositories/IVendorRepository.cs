using VendorService.API.Models;

namespace VendorService.API.Repositories
{
    public interface IVendorRepository
    {
        Task<(List<Vendor> Vendors, int Total)> GetPagedAsync(
            int page,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken);

        Task<Vendor?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<Vendor> CreateAsync(
            Vendor vendor,
            CancellationToken cancellationToken);

        Task<Vendor> UpdateAsync(
            Vendor vendor,
            CancellationToken cancellationToken);
    }
}