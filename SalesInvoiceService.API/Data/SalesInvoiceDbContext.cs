using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Data
{
    public class SalesInvoiceDbContext : DbContext
    {
        public SalesInvoiceDbContext(
            DbContextOptions<SalesInvoiceDbContext> options)
            : base(options)
        {
        }

        public DbSet<SalesInvoice> SalesInvoices =>
            Set<SalesInvoice>();

        public DbSet<SalesInvoiceItem> SalesInvoiceItems =>
            Set<SalesInvoiceItem>();

        public DbSet<CurrencyMaster> CurrencyMasters =>
            Set<CurrencyMaster>();

        public DbSet<PaymentMode> PaymentModes =>
            Set<PaymentMode>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.ToTable("SalesInvoices", "sales");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => x.ClientId);

                entity.HasIndex(x => x.InvoiceNumber)
                    .IsUnique()
                    .HasFilter("[InvoiceNumber] IS NOT NULL");

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
                    .WithOne(x => x.SalesInvoice)
                    .HasForeignKey(x => x.SalesInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalesInvoiceItem>(entity =>
            {
                entity.ToTable("SalesInvoiceItems", "sales");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => new
                {
                    x.SalesInvoiceId,
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

            modelBuilder.Entity<CurrencyMaster>(entity =>
            {
                entity.ToTable("CurrencyMasters", "sales");

                entity.HasKey(x => x.CurrencyCode);

                entity.Property(x => x.CurrencyCode)
                    .HasMaxLength(3)
                    .ValueGeneratedNever();

                entity.Property(x => x.CurrencyName)
                    .IsRequired()
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<PaymentMode>(entity =>
            {
                entity.ToTable("PaymentModes", "sales");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(x => x.Name)
                    .IsUnique();
            });
        }
    }
}