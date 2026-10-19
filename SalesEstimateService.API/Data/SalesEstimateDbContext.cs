using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.Data
{
    public class SalesEstimateDbContext : DbContext
    {
        public SalesEstimateDbContext(
            DbContextOptions<SalesEstimateDbContext> options)
            : base(options)
        {
        }

        public DbSet<SalesEstimate> SalesEstimates =>
            Set<SalesEstimate>();

        public DbSet<SalesEstimateItem> SalesEstimateItems =>
            Set<SalesEstimateItem>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SalesEstimate>(entity =>
            {
                entity.ToTable("SalesEstimates", "estimates");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => x.ClientId);

                entity.HasIndex(x => x.EstimateNumber)
                    .IsUnique()
                    .HasFilter("[EstimateNumber] IS NOT NULL");

                entity.HasIndex(x => x.ConvertedInvoiceId)
                    .IsUnique()
                    .HasFilter("[ConvertedInvoiceId] IS NOT NULL");

                entity.Property(x => x.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(x => x.SubTotal)
                    .HasPrecision(18, 4);

                entity.Property(x => x.DiscountTotal)
                    .HasPrecision(18, 4);

                entity.Property(x => x.TaxTotal)
                    .HasPrecision(18, 4);

                entity.Property(x => x.GrandTotal)
                    .HasPrecision(18, 4);

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasMany(x => x.Items)
                    .WithOne(x => x.SalesEstimate)
                    .HasForeignKey(x => x.SalesEstimateId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EstimateNumberCounter>(entity =>
            {
                entity.ToTable("EstimateNumberCounters", "estimates");

                entity.HasKey(x => x.PeriodKey);

                entity.Property(x => x.PeriodKey)
                    .HasMaxLength(6);
            });

            modelBuilder.Entity<SalesEstimateItem>(entity =>
            {
                entity.ToTable("SalesEstimateItems", "estimates");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => new
                {
                    x.SalesEstimateId,
                    x.LineNumber
                })
                    .IsUnique();

                entity.Property(x => x.Unit)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(x => x.Quantity)
                    .HasPrecision(18, 4);

                entity.Property(x => x.UnitPrice)
                    .HasPrecision(18, 4);

                entity.Property(x => x.DiscountEnabled)
                    .HasDefaultValue(false);

                entity.Property(x => x.DiscountAmount)
                    .HasPrecision(18, 4);

                entity.Property(x => x.TaxRate)
                    .HasPrecision(7, 4);

                entity.Property(x => x.LineSubTotal)
                    .HasPrecision(18, 4);

                entity.Property(x => x.TaxAmount)
                    .HasPrecision(18, 4);

                entity.Property(x => x.LineTotal)
                    .HasPrecision(18, 4);
            });
        }
    }
}
