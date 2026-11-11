using SparkFinance.Auditing;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Data
{
    public class SalesInvoiceDbContext : AuditedDbContext
    {
        public SalesInvoiceDbContext(
            DbContextOptions<SalesInvoiceDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrganizationProfile> OrganizationProfiles => Set<OrganizationProfile>();

        public DbSet<SalesInvoice> SalesInvoices =>
            Set<SalesInvoice>();

        public DbSet<SalesInvoiceItem> SalesInvoiceItems =>
            Set<SalesInvoiceItem>();

        public DbSet<CurrencyMaster> CurrencyMasters =>
            Set<CurrencyMaster>();

        public DbSet<PaymentMode> PaymentModes =>
            Set<PaymentMode>();

        public DbSet<BranchMaster> BranchMasters =>
            Set<BranchMaster>();

        public DbSet<SalesInvoicePayment> SalesInvoicePayments =>
            Set<SalesInvoicePayment>();

        public DbSet<SalesCreditNote> SalesCreditNotes =>
            Set<SalesCreditNote>();

        public DbSet<CustomerCreditApplication> CustomerCreditApplications =>
            Set<CustomerCreditApplication>();

        public DbSet<SalesCreditNoteItem> SalesCreditNoteItems =>
            Set<SalesCreditNoteItem>();

        public DbSet<CreditNoteNumberCounter> CreditNoteNumberCounters =>
            Set<CreditNoteNumberCounter>();
         
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OrganizationProfile>().ToTable(
                "OrganizationProfiles", "sales", table => table.ExcludeFromMigrations());


            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.ToTable("SalesInvoices", "sales");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => x.ClientId);

                entity.HasIndex(x => x.SourceEstimateId)
                    .IsUnique()
                    .HasFilter("[SourceEstimateId] IS NOT NULL");

                entity.HasIndex(x => x.InvoiceNumber)
                    .IsUnique()
                    .HasFilter("[InvoiceNumber] IS NOT NULL");

                entity.Property(x => x.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(x => x.TaxTreatment)
                    .HasConversion<string>()
                    .HasMaxLength(30);

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
                entity.Property(row => row.DiscountValue).HasPrecision(18, 4);
                entity.Property(row => row.DiscountType).HasMaxLength(10).HasDefaultValue("Fixed");
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

                entity.Property(x => x.DiscountEnabled)
                    .HasDefaultValue(true);

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

            modelBuilder.Entity<SalesInvoicePayment>(entity =>
            {
                entity.Property(row => row.BankAmount).HasPrecision(18, 4);
                entity.Property(row => row.BankChargeAmount)
                    .HasPrecision(18, 4);
                entity.HasIndex(row => row.BankTransactionId).IsUnique()
                    .HasFilter("[BankTransactionId] IS NOT NULL");
                entity.ToTable("SalesInvoicePayments", "sales");
                entity.Property(row => row.Status).HasDefaultValue("Recorded");
                // Financial queries exclude voided receipts. History reads opt in explicitly.
                entity.HasQueryFilter(row => row.Status == "Recorded");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => x.RequestId)
                    .IsUnique();

                entity.HasIndex(x => new
                {
                    x.SalesInvoiceId,
                    x.PaymentDate
                });

                entity.Property(x => x.Amount)
                    .HasPrecision(18, 4);

                entity.Property(x => x.Currency)
                    .IsRequired()
                    .HasMaxLength(3);

                entity.Property(x => x.PaymentMode)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(x => x.Reference)
                    .HasMaxLength(200);

                entity.Property(x => x.Notes)
                    .HasMaxLength(2000);

                entity.HasOne(x => x.SalesInvoice)
                    .WithMany()
                    .HasForeignKey(x => x.SalesInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalesCreditNote>(entity =>
            {
                entity.ToTable("SalesCreditNotes", "sales");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => x.RequestId)
                    .IsUnique();

                entity.HasIndex(x => x.CreditNoteNumber)
                    .IsUnique()
                    .HasFilter("[CreditNoteNumber] IS NOT NULL");

                entity.HasIndex(x => new
                {
                    x.SalesInvoiceId,
                    x.CreditNoteDate
                });

                entity.Property(x => x.CreditNoteNumber)
                    .HasMaxLength(50);

                entity.Property(x => x.Reason)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(x => x.Notes)
                    .HasMaxLength(2000);

                entity.Property(x => x.Currency)
                    .IsRequired()
                    .HasMaxLength(3);

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

                entity.HasOne(x => x.SalesInvoice)
                    .WithMany()
                    .HasForeignKey(x => x.SalesInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(x => x.Items)
                    .WithOne(x => x.SalesCreditNote)
                    .HasForeignKey(x => x.SalesCreditNoteId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalesCreditNoteItem>(entity =>
            {
                entity.ToTable("SalesCreditNoteItems", "sales");

                entity.Property(x => x.CreditAmount).HasPrecision(18, 4);

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.HasIndex(x => new
                {
                    x.SalesCreditNoteId,
                    x.LineNumber
                })
                    .IsUnique();

                entity.HasIndex(x => new
                {
                    x.SalesCreditNoteId,
                    x.SalesInvoiceItemId
                })
                    .IsUnique();

                entity.Property(x => x.ServiceName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(x => x.Description)
                    .HasMaxLength(1000);

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

                entity.HasOne<SalesInvoiceItem>()
                    .WithMany()
                    .HasForeignKey(x => x.SalesInvoiceItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomerCreditApplication>(entity =>
            {
                entity.ToTable(
                    "CustomerCreditApplications",
                    "sales",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_CustomerCreditApplications_Amount",
                            "[Amount] > 0");

                        table.HasCheckConstraint(
                            "CK_CustomerCreditApplications_Invoices",
                            "[SourceInvoiceId] <> [TargetInvoiceId]");
                    });

                entity.HasKey(application =>
                    application.Id);

                entity.Property(application =>
                    application.Id)
                    .UseIdentityColumn();

                entity.HasIndex(application =>
                    application.RequestId)
                    .IsUnique();

                entity.HasIndex(application => new
                {
                    application.SourceInvoiceId,
                    application.AppliedAtUtc
                });

                entity.HasIndex(application => new
                {
                    application.TargetInvoiceId,
                    application.AppliedAtUtc
                });

                entity.Property(application =>
                    application.Amount)
                    .HasPrecision(18, 4);

                entity.Property(application =>
                    application.Branch)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(application =>
                    application.Currency)
                    .HasMaxLength(3)
                    .IsRequired();

                entity.HasOne(application =>
                    application.SourceInvoice)
                    .WithMany()
                    .HasForeignKey(application =>
                        application.SourceInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(application =>
                    application.TargetInvoice)
                    .WithMany()
                    .HasForeignKey(application =>
                        application.TargetInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<InvoiceNumberCounter>(entity =>
            {
                entity.ToTable("InvoiceNumberCounters", "sales");

                entity.HasKey(x => x.PeriodKey);

                entity.Property(x => x.PeriodKey)
                    .HasMaxLength(6);
            });

            modelBuilder.Entity<CreditNoteNumberCounter>(entity =>
            {
                entity.ToTable("CreditNoteNumberCounters", "sales");

                entity.HasKey(x => x.PeriodKey);

                entity.Property(x => x.PeriodKey)
                    .HasMaxLength(6)
                    .ValueGeneratedNever();
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

            modelBuilder.Entity<BranchMaster>(entity =>
            {
                entity.ToTable("BranchMasters", "sales");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .UseIdentityColumn();

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(x => x.CompanyName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.Address)
                    .IsRequired()
                    .HasMaxLength(1000)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.CountryCode)
                    .IsRequired()
                    .HasMaxLength(2)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.CompanyRegistrationNumber)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.TaxRegistrationNumber)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.BankAccountName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.BankName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.BankAccountNumber)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.BankIban)
                    .IsRequired()
                    .HasMaxLength(34)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.BankSwiftCode)
                    .IsRequired()
                    .HasMaxLength(11)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.Phone)
                    .IsRequired()
                    .HasMaxLength(25)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.Email)
                    .IsRequired()
                    .HasMaxLength(254)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.InvoiceNotes)
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasDefaultValue(string.Empty);

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => x.Name)
                    .IsUnique();
            });
        }
    }
}
