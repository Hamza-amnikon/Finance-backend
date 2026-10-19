using Microsoft.EntityFrameworkCore;
using VendorService.API.Data;
using VendorService.API.Models;

namespace VendorService.API.Repositories
{
    public class VendorRepository : IVendorRepository
    {
        private readonly VendorDbContext _context;

        public VendorRepository(VendorDbContext context)
        {
            _context = context;
        }

        public async Task<(List<Vendor> Vendors, int Total)> GetPagedAsync(
            int page,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken)
        {
            if (page < 1)
            {
                throw new ArgumentException(
                    "Page must be greater than zero.",
                    nameof(page));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                throw new ArgumentException(
                    "Page size must be between 1 and 100.",
                    nameof(pageSize));
            }

            var searchText = search?.Trim();

            if (searchText?.Length > 200)
            {
                throw new ArgumentException(
                    "Search cannot exceed 200 characters.",
                    nameof(search));
            }

            var query = _context.Vendors.AsNoTracking();

            if (isActive.HasValue)
            {
                query = query.Where(
                    vendor => vendor.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(vendor =>
                    vendor.Name.Contains(searchText) ||
                    (vendor.CompanyName != null &&
                        vendor.CompanyName.Contains(searchText)) ||
                    (vendor.Email != null &&
                        vendor.Email.Contains(searchText)) ||
                    (vendor.Phone != null &&
                        vendor.Phone.Contains(searchText)));
            }

            var total = await query.CountAsync(cancellationToken);

            var offset = ((long)page - 1) * pageSize;

            if (offset >= total)
            {
                return (new List<Vendor>(), total);
            }

            var vendors = await query
                .OrderByDescending(vendor => vendor.CreatedDate)
                .ThenByDescending(vendor => vendor.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (vendors, total);
        }

        public async Task<Vendor?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return await _context.Vendors
                .SingleOrDefaultAsync(
                    vendor => vendor.Id == id,
                    cancellationToken);
        }

        public async Task<Vendor> CreateAsync(
            Vendor vendor,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(vendor);

            _context.Vendors.Add(vendor);

            await _context.SaveChangesAsync(cancellationToken);

            return vendor;
        }

        public async Task<Vendor> UpdateAsync(
            Vendor vendor,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(vendor);

            var entry = _context.Entry(vendor);

            if (entry.State == EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "Load the vendor through GetByIdAsync before updating it.");
            }

            // Match the row version supplied by the caller.
            // EF throws DbUpdateConcurrencyException if it is outdated.
            entry.Property(current => current.RowVersion)
                .OriginalValue = vendor.RowVersion;

            await _context.SaveChangesAsync(cancellationToken);

            return vendor;
        }
    }
}