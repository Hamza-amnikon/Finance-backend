using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.Data
{
    public class VendorCreditDbContext : DbContext
    {
        public VendorCreditDbContext(
            DbContextOptions<VendorCreditDbContext> options)
            : base(options)
        {
        }

        public DbSet<VendorCredit> VendorCredits =>
            Set<VendorCredit>();

        public DbSet<VendorCreditItem> VendorCreditItems =>
            Set<VendorCreditItem>();

        public DbSet<VendorCreditNumberCounter> NumberCounters =>
            Set<VendorCreditNumberCounter>();

        public DbSet<PurchaseVendorLookup> Vendors =>
            Set<PurchaseVendorLookup>();

        public DbSet<PurchaseCurrencyLookup> Currencies =>
            Set<PurchaseCurrencyLookup>();

        public DbSet<PurchaseBranchLookup> Branches =>
            Set<PurchaseBranchLookup>();

        public DbSet<PurchaseBillLookup> Bills =>
            Set<PurchaseBillLookup>();

        public DbSet<VendorCreditApplication> Applications =>
            Set<VendorCreditApplication>();

        public DbSet<CreditPaymentLookup> Payments =>
            Set<CreditPaymentLookup>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<VendorCredit>(entity =>
            {
                entity.ToTable(
                    "VendorCredits",
                    "purchases");

                entity.HasKey(row => row.Id);

                entity.HasIndex(row => row.RequestId)
                    .IsUnique();

                entity.HasIndex(row => row.VendorId);

                entity.HasIndex(row => row.BillId);

                entity.HasIndex(row => row.VendorCreditNumber)
                    .IsUnique()
                    .HasFilter(
                        "[VendorCreditNumber] IS NOT NULL");

                entity.HasIndex(row => new
                {
                    row.VendorId,
                    row.VendorCreditNoteNumber
                })
                    .IsUnique()
                    .HasFilter(
                        "[VendorCreditNoteNumber] <> N'' AND [Status] <> N'Cancelled'");

                entity.Property(row => row.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(row => row.RowVersion)
                    .IsRowVersion();

                entity.Property(row => row.SubTotal)
                    .HasPrecision(18, 4);

                entity.Property(row => row.DiscountTotal)
                    .HasPrecision(18, 4);

                entity.Property(row => row.TaxTotal)
                    .HasPrecision(18, 4);

                entity.Property(row => row.GrandTotal)
                    .HasPrecision(18, 4);

                entity.HasMany(row => row.Items)
                    .WithOne(row => row.VendorCredit)
                    .HasForeignKey(row => row.VendorCreditId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<VendorCreditItem>(entity =>
            {
                entity.ToTable(
                    "VendorCreditItems",
                    "purchases");

                entity.HasKey(row => row.Id);

                entity.HasIndex(row => new
                {
                    row.VendorCreditId,
                    row.LineNumber
                })
                    .IsUnique();

                entity.Property(row => row.Unit)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                foreach (var property in new[]
                {
                    "Quantity",
                    "UnitPrice",
                    "DiscountAmount",
                    "TaxRate",
                    "LineSubTotal",
                    "TaxAmount",
                    "LineTotal"
                })
                {
                    entity.Property<decimal>(property)
                        .HasPrecision(18, 4);
                }
            });

            builder.Entity<VendorCreditNumberCounter>(entity =>
            {
                entity.ToTable(
                    "VendorCreditNumberCounters",
                    "purchases");

                entity.HasKey(row => row.PeriodKey);

                entity.Property(row => row.PeriodKey)
                    .HasMaxLength(6)
                    .IsRequired();
            });

            builder.Entity<VendorCreditApplication>(entity =>
            {
                entity.ToTable("VendorCreditApplications", "purchases");
                entity.HasKey(row => row.Id);
                entity.HasIndex(row => row.RequestId).IsUnique();
                entity.HasIndex(row => new { row.VendorCreditId, row.Status });
                entity.HasIndex(row => new { row.BillId, row.Status });
                entity.Property(row => row.Amount).HasPrecision(18, 4);
                entity.Property(row => row.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(row => row.RowVersion).IsRowVersion();
                entity.HasOne<VendorCredit>()
                    .WithMany()
                    .HasForeignKey(row => row.VendorCreditId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CreditPaymentLookup>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("PaymentsMade", "purchases");
                entity.Property(row => row.Amount).HasPrecision(18, 4);
                entity.Property(row => row.Status).HasMaxLength(20);
            });

            // Read existing shared tables without owning their migrations.
            builder.Entity<PurchaseVendorLookup>()
                .HasNoKey()
                .ToView(
                    "VendorMaster",
                    "purchases");

            builder.Entity<PurchaseCurrencyLookup>()
                .HasNoKey()
                .ToView(
                    "CurrencyMasters",
                    "sales");

            builder.Entity<PurchaseBranchLookup>()
                .HasNoKey()
                .ToView(
                    "BranchMasters",
                    "sales");

            builder.Entity<PurchaseBillLookup>(entity =>
            {
                entity.HasNoKey();

                entity.ToView(
                    "Bills",
                    "purchases");

                entity.Property(row => row.Status)
                    .HasMaxLength(20);

                entity.Property(row => row.GrandTotal)
                    .HasPrecision(18, 4);
            });
        }
    }
}
