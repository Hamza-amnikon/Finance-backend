using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using VendorService.API.Models;

namespace VendorService.API.Data
{
    public class VendorDbContext : DbContext
    {
        public VendorDbContext(
            DbContextOptions<VendorDbContext> options)
            : base(options)
        {
        }

        public DbSet<Vendor> Vendors => Set<Vendor>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Vendor>(entity =>
            {
                entity.ToTable("VendorMaster", "purchases");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .ValueGeneratedNever();

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => x.Name);

                entity.HasIndex(x => new
                {
                    x.Branch,
                    x.IsActive
                });
            });
        }
    }
}