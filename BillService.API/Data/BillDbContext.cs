using Microsoft.EntityFrameworkCore;
using BillService.API.Models;

namespace BillService.API.Data
{
    public class BillDbContext : DbContext
    {
        public BillDbContext(
            DbContextOptions<BillDbContext> options)
            : base(options)
        {
        }

        public DbSet<Bill> Bills => Set<Bill>();
        public DbSet<BillItem> BillItems => Set<BillItem>();
        public DbSet<BillNumberCounter> NumberCounters => Set<BillNumberCounter>();
        public DbSet<PurchaseVendorLookup> Vendors => Set<PurchaseVendorLookup>();
        public DbSet<PurchaseCurrencyLookup> Currencies => Set<PurchaseCurrencyLookup>();
        public DbSet<PurchaseBranchLookup> Branches => Set<PurchaseBranchLookup>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Bill>(entity =>
            {
                entity.ToTable("Bills", "purchases");
                entity.HasKey(row => row.Id);
                entity.HasIndex(row => row.RequestId).IsUnique();
                entity.HasIndex(row => row.VendorId);
                entity.HasIndex(row => row.SourcePurchaseOrderId).IsUnique().HasFilter("[SourcePurchaseOrderId] IS NOT NULL");
                entity.HasIndex(row => new { row.VendorId, row.VendorInvoiceNumber }).IsUnique()
                    .HasFilter("[VendorInvoiceNumber] <> N'' AND [Status] <> N'Cancelled'");
                entity.HasIndex(row => row.BillNumber).IsUnique().HasFilter("[BillNumber] IS NOT NULL");
                entity.Property(row => row.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(row => row.RowVersion).IsRowVersion();
                entity.Property(row => row.SubTotal).HasPrecision(18, 4);
                entity.Property(row => row.DiscountTotal).HasPrecision(18, 4);
                entity.Property(row => row.TaxTotal).HasPrecision(18, 4);
                entity.Property(row => row.GrandTotal).HasPrecision(18, 4);
                entity.HasMany(row => row.Items).WithOne(row => row.Bill)
                    .HasForeignKey(row => row.BillId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<BillItem>(entity =>
            {
                entity.ToTable("BillItems", "purchases");
                entity.HasKey(row => row.Id);
                entity.HasIndex(row => new { row.BillId, row.LineNumber }).IsUnique();
                entity.Property(row => row.Unit).HasConversion<string>().HasMaxLength(20);
                foreach (var property in new[] { "Quantity", "UnitPrice", "DiscountAmount", "TaxRate", "LineSubTotal", "TaxAmount", "LineTotal" })
                    entity.Property<decimal>(property).HasPrecision(18, 4);
            });
            builder.Entity<BillNumberCounter>(entity =>
            {
                entity.ToTable("BillNumberCounters", "purchases");
                entity.HasKey(row => row.PeriodKey);
            });

            // Read-only mappings over existing shared tables; migrations do not own them.
            builder.Entity<PurchaseVendorLookup>().HasNoKey().ToView("VendorMaster", "purchases");
            builder.Entity<PurchaseCurrencyLookup>().HasNoKey().ToView("CurrencyMasters", "sales");
            builder.Entity<PurchaseBranchLookup>().HasNoKey().ToView("BranchMasters", "sales");
        }
    }
}
