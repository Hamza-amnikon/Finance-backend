using SparkFinance.Database;
using SparkFinance.Auditing;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using System.Security.Cryptography;
using VendorService.API.Models;

namespace VendorService.API.Data
{
    public class VendorDbContext : AuditedDbContext
    {
        public VendorDbContext(
            DbContextOptions<VendorDbContext> options)
            : base(options)
        {
        }

        public DbSet<Vendor> Vendors => Set<Vendor>();

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (Database.IsFinanceMySql())
            {
                foreach (var entry in ChangeTracker.Entries<Vendor>()
                    .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
                {
                    var version = entry.Property(vendor => vendor.RowVersion);
                    version.CurrentValue = RandomNumberGenerator.GetBytes(8);
                    if (entry.State == EntityState.Modified)
                    {
                        version.IsModified = true;
                    }
                }
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

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

                var rowVersion = entity.Property(x => x.RowVersion);
                if (Database.IsFinanceMySql())
                {
                    rowVersion.HasColumnType("binary(8)")
                        .IsConcurrencyToken()
                        .ValueGeneratedNever();
                }
                else
                {
                    rowVersion.IsRowVersion();
                }

                entity.HasIndex(x => x.Name);

                entity.HasIndex(x => new
                {
                    x.Branch,
                    x.IsActive
                });
            });

            modelBuilder.MapFinanceMySql(Database);
        }
    }
}
