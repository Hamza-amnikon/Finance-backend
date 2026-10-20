using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.Data
{
    public class PurchaseOrderDbContext : DbContext
    {
        public PurchaseOrderDbContext(
            DbContextOptions<PurchaseOrderDbContext> options)
            : base(options)
        {
        }

        public DbSet<PurchaseOrder> PurchaseOrders =>
            Set<PurchaseOrder>();

        public DbSet<PurchaseOrderItem> PurchaseOrderItems =>
            Set<PurchaseOrderItem>();

        public DbSet<PurchaseOrderNumberCounter> NumberCounters =>
            Set<PurchaseOrderNumberCounter>();

        public DbSet<PurchaseVendorLookup> Vendors =>
            Set<PurchaseVendorLookup>();

        public DbSet<PurchaseCurrencyLookup> Currencies =>
            Set<PurchaseCurrencyLookup>();

        public DbSet<PurchaseBranchLookup> Branches =>
            Set<PurchaseBranchLookup>();

        public DbSet<PurchasePaymentModeLookup> PaymentModes =>
            Set<PurchasePaymentModeLookup>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PurchaseOrder>(entity =>
            {
                entity.ToTable("PurchaseOrders", "purchases");

                entity.HasKey(row => row.Id);

                entity.HasIndex(row => row.RequestId)
                    .IsUnique();

                entity.HasIndex(row => row.VendorId);

                entity.HasIndex(row => row.PurchaseOrderNumber)
                    .IsUnique()
                    .HasFilter("[PurchaseOrderNumber] IS NOT NULL");

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
                    .WithOne(row => row.PurchaseOrder)
                    .HasForeignKey(row => row.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PurchaseOrderItem>(entity =>
            {
                entity.ToTable("PurchaseOrderItems", "purchases");

                entity.HasKey(row => row.Id);

                entity.HasIndex(row => new
                {
                    row.PurchaseOrderId,
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

            builder.Entity<PurchaseOrderNumberCounter>(entity =>
            {
                entity.ToTable(
                    "PurchaseOrderNumberCounters",
                    "purchases");

                entity.HasKey(row => row.PeriodKey);
            });

            // Existing shared tables.
            builder.Entity<PurchaseVendorLookup>()
                .HasNoKey()
                .ToView("VendorMaster", "purchases");

            builder.Entity<PurchaseCurrencyLookup>()
                .HasNoKey()
                .ToView("CurrencyMasters", "sales");

            builder.Entity<PurchaseBranchLookup>()
                .HasNoKey()
                .ToView("BranchMasters", "sales");

            builder.Entity<PurchasePaymentModeLookup>(entity =>
            {
                entity.ToTable(
                    "PaymentModes",
                    "sales",
                    table => table.ExcludeFromMigrations());

                entity.HasKey(mode => mode.Id);

                entity.Property(mode => mode.Name)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(mode => mode.IsActive)
                    .IsRequired();
            });
        }
    }
}