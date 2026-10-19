using Microsoft.EntityFrameworkCore;
using VendorService.API.Models;

namespace VendorService.API.Data
{
    public class VendorLocationDbContext : DbContext
    {
        public VendorLocationDbContext(
            DbContextOptions<VendorLocationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CountryMasterLookup> CountryMasters =>
            Set<CountryMasterLookup>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CountryMasterLookup>(entity =>
            {
                entity.HasNoKey();

                // ToView also supports querying an existing physical table.
                // This mapping is read-only and does not create/own that table.
                entity.ToView("countrymasters", "dbo");

                entity.Property(row => row.CountryName).HasMaxLength(100);
                entity.Property(row => row.State).HasMaxLength(100);
                entity.Property(row => row.City).HasMaxLength(100);
                entity.Property(row => row.CurrencyCode).HasMaxLength(3);
            });
        }
    }
}
