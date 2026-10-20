using Microsoft.EntityFrameworkCore;
using PaymentMadeService.API.Models;

namespace PaymentMadeService.API.Data
{
    public class PaymentMadeDbContext : DbContext
    {
        public PaymentMadeDbContext(
            DbContextOptions<PaymentMadeDbContext> options)
            : base(options)
        {
        }

        public DbSet<PaymentMade> PaymentsMade => Set<PaymentMade>();

        public DbSet<PaymentMadeNumberCounter> NumberCounters =>
            Set<PaymentMadeNumberCounter>();

        public DbSet<PaymentBillLookup> Bills => Set<PaymentBillLookup>();

        public DbSet<PaymentVendorLookup> Vendors => Set<PaymentVendorLookup>();

        public DbSet<PaymentModeLookup> PaymentModes => Set<PaymentModeLookup>();

        public DbSet<PaymentCreditApplicationLookup> CreditApplications =>
            Set<PaymentCreditApplicationLookup>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PaymentMade>(entity =>
            {
                entity.ToTable("PaymentsMade", "purchases");
                entity.HasKey(row => row.Id);
                entity.HasIndex(row => row.RequestId).IsUnique();
                entity.HasIndex(row => row.PaymentNumber).IsUnique();
                entity.HasIndex(row => row.VendorId);
                entity.HasIndex(row => new { row.BillId, row.Status });
                entity.Property(row => row.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(row => row.Amount).HasPrecision(18, 4);
                entity.Property(row => row.RowVersion).IsRowVersion();
            });

            builder.Entity<PaymentMadeNumberCounter>(entity =>
            {
                entity.ToTable("PaymentMadeNumberCounters", "purchases");
                entity.HasKey(row => row.PeriodKey);
            });

            builder.Entity<PaymentCreditApplicationLookup>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("VendorCreditApplications", "purchases");
                entity.Property(row => row.Amount).HasPrecision(18, 4);
                entity.Property(row => row.Status).HasMaxLength(20);
            });

            // Shared reference tables remain owned by their existing services.
            builder.Entity<PaymentBillLookup>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("Bills", "purchases");
                entity.Property(row => row.GrandTotal).HasPrecision(18, 4);
                entity.Property(row => row.Status).HasMaxLength(20);
            });

            builder.Entity<PaymentVendorLookup>()
                .HasNoKey()
                .ToView("VendorMaster", "purchases");

            builder.Entity<PaymentModeLookup>()
                .HasNoKey()
                .ToView("PaymentModes", "sales");
        }
    }
}
