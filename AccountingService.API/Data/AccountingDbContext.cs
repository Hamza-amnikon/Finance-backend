using SparkFinance.Auditing;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Data
{
    public class AccountingDbContext : AuditedDbContext
    {
        public AccountingDbContext(
            DbContextOptions<AccountingDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts =>
            Set<Account>();

        public DbSet<AccountingCompanyLookup> Companies =>
            Set<AccountingCompanyLookup>();

        public DbSet<JournalEntry> JournalEntries =>
            Set<JournalEntry>();

        public DbSet<JournalEntryLine> JournalEntryLines =>
            Set<JournalEntryLine>();
        public DbSet<TaxConfigurationType> TaxTypes => Set<TaxConfigurationType>();
        public DbSet<TaxConfigurationRate> TaxRates => Set<TaxConfigurationRate>();

        public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TaxConfigurationType>(entity =>
            {
                entity.HasIndex(row => new { row.CountryCode, row.Code }).IsUnique();
            });
            modelBuilder.Entity<TaxConfigurationRate>(entity =>
            {
                entity.HasIndex(row => new { row.CompanyId, row.Code, row.EffectiveFrom }).IsUnique();
                entity.HasOne<AccountingCompanyLookup>().WithMany().HasForeignKey(row => row.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<TaxConfigurationType>().WithMany().HasForeignKey(row => row.TaxTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AuditEvent>(entity =>
            {
                entity.ToTable("Events", "audit");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Service).HasMaxLength(100);
                entity.Property(row => row.EntityType).HasMaxLength(100);
                entity.Property(row => row.EntityId).HasMaxLength(200);
                entity.Property(row => row.Action).HasMaxLength(40);
                entity.Property(row => row.Actor).HasMaxLength(200);
                entity.Property(row => row.ActorKind).HasMaxLength(50);
                entity.Property(row => row.Branch).HasMaxLength(100);
                entity.Property(row => row.CorrelationId).HasMaxLength(100);
                entity.HasIndex(row => row.AtUtc);
                entity.HasIndex(row => new { row.EntityType, row.EntityId });
                entity.HasIndex(row => new { row.Branch, row.AtUtc });
            });

            modelBuilder.HasDefaultSchema(
                "accounting");

            ConfigureCompanies(modelBuilder);
            ConfigureAccounts(modelBuilder);
            ConfigureJournalEntries(modelBuilder);
            ConfigureJournalEntryLines(modelBuilder);
        }

        private static void ConfigureCompanies(
            ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<AccountingCompanyLookup>(
                    entity =>
                    {
                        entity.ToTable(
                            "Companies",
                            "expenses",
                            table =>
                                table.ExcludeFromMigrations());

                        entity.HasKey(
                            company => company.Id);

                        entity.Property(
                                company => company.Name)
                            .HasMaxLength(160)
                            .IsRequired();

                        entity.Property(
                                company => company.Country)
                            .HasMaxLength(2)
                            .IsRequired();

                        entity.Property(
                                company => company.Currency)
                            .HasMaxLength(3)
                            .IsRequired();
                    });
        }

        private static void ConfigureAccounts(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable(
                    "Accounts",
                    "accounting",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_Accounts_Code",
                            "LEN(LTRIM(RTRIM([Code]))) > 0");

                        table.HasCheckConstraint(
                            "CK_Accounts_Parent",
                            "[ParentAccountId] IS NULL OR " +
                            "[ParentAccountId] <> [Id]");
                    });

                entity.HasKey(
                    account => account.Id);

                entity.Property(
                        account => account.Code)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(
                        account => account.Name)
                    .HasMaxLength(160)
                    .IsRequired();

                entity.Property(
                        account => account.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(
                        account => account.NormalBalance)
                    .HasConversion<string>()
                    .HasMaxLength(10)
                    .IsRequired();

                entity.Property(
                        account => account.Description)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(
                        account => account.SystemKey)
                    .HasMaxLength(60);

                entity.Property(
                        account => account.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(account => new
                {
                    account.CompanyId,
                    account.Code
                })
                    .IsUnique();

                entity.HasIndex(account => new
                {
                    account.CompanyId,
                    account.SystemKey
                })
                    .IsUnique()
                    .HasFilter(
                        "[SystemKey] IS NOT NULL");

                entity.HasAlternateKey(account => new
                {
                    account.Id,
                    account.CompanyId
                });

                entity.HasOne(
                        account => account.Company)
                    .WithMany(
                        company => company.Accounts)
                    .HasForeignKey(
                        account => account.CompanyId)
                    .OnDelete(
                        DeleteBehavior.Restrict);

                entity.HasOne(
                        account => account.ParentAccount)
                    .WithMany(
                        account => account.ChildAccounts)
                    .HasForeignKey(account => new
                    {
                        account.ParentAccountId,
                        account.CompanyId
                    })
                    .HasPrincipalKey(account => new
                    {
                        account.Id,
                        account.CompanyId
                    })
                    .OnDelete(
                        DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureJournalEntries(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.ToTable(
                    "JournalEntries",
                    "accounting",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_JournalEntries_EntryNumber",
                            "LEN(LTRIM(RTRIM([EntryNumber]))) > 0");

                        table.HasCheckConstraint(
                            "CK_JournalEntries_ExchangeRate",
                            "[ExchangeRate] > 0");

                        table.HasCheckConstraint(
                            "CK_JournalEntries_Totals",
                            "[TotalDebit] >= 0 AND " +
                            "[TotalCredit] >= 0");

                        table.HasCheckConstraint(
                            "CK_JournalEntries_PostedBalance",
                            "[Status] = N'Draft' OR " +
                            "[TotalDebit] = [TotalCredit]");

                        table.HasCheckConstraint(
                            "CK_JournalEntries_Reversal",
                            "[ReversalOfJournalEntryId] IS NULL OR " +
                            "[ReversalOfJournalEntryId] <> [Id]");
                    });

                entity.HasKey(
                    entry => entry.Id);

                entity.HasAlternateKey(entry => new
                {
                    entry.Id,
                    entry.CompanyId
                });

                entity.Property(
                        entry => entry.EntryNumber)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(
                        entry => entry.Branch)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(
                        entry => entry.Currency)
                    .HasMaxLength(3)
                    .IsRequired();

                entity.Property(
                        entry => entry.ExchangeRate)
                    .HasPrecision(18, 8);

                entity.Property(
                        entry => entry.Narration)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(
                        entry => entry.Reference)
                    .HasMaxLength(100);

                entity.Property(
                        entry => entry.Source)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(
                        entry => entry.SourceDocumentId)
                    .HasMaxLength(100);

                entity.Property(
                        entry => entry.SourceDocumentNumber)
                    .HasMaxLength(100);

                entity.Property(
                        entry => entry.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(
                        entry => entry.TotalDebit)
                    .HasPrecision(19, 4);

                entity.Property(
                        entry => entry.TotalCredit)
                    .HasPrecision(19, 4);

                entity.Property(
                        entry => entry.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(entry => new
                {
                    entry.CompanyId,
                    entry.EntryNumber
                })
                    .IsUnique();

                entity.HasIndex(entry => new
                {
                    entry.CompanyId,
                    entry.Source,
                    entry.SourceDocumentId
                })
                    .IsUnique()
                    .HasFilter(
                        "[SourceDocumentId] IS NOT NULL");

                entity.HasIndex(entry => new
                {
                    entry.CompanyId,
                    entry.EntryDate,
                    entry.Status
                });

                entity.HasOne(
                        entry => entry.Company)
                    .WithMany()
                    .HasForeignKey(
                        entry => entry.CompanyId)
                    .OnDelete(
                        DeleteBehavior.Restrict);

                entity.HasOne(
                        entry => entry.ReversalOfJournalEntry)
                    .WithMany(
                        entry => entry.ReversalEntries)
                    .HasForeignKey(entry => new
                    {
                        entry.ReversalOfJournalEntryId,
                        entry.CompanyId
                    })
                    .HasPrincipalKey(entry => new
                    {
                        entry.Id,
                        entry.CompanyId
                    })
                    .OnDelete(
                        DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureJournalEntryLines(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JournalEntryLine>(entity =>
            {
                entity.ToTable(
                    "JournalEntryLines",
                    "accounting",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_JournalEntryLines_LineNumber",
                            "[LineNumber] > 0");

                        table.HasCheckConstraint(
                            "CK_JournalEntryLines_Amount",
                            "(" +
                            "[DebitAmount] > 0 AND " +
                            "[CreditAmount] = 0" +
                            ") OR (" +
                            "[CreditAmount] > 0 AND " +
                            "[DebitAmount] = 0" +
                            ")");

                        table.HasCheckConstraint(
                            "CK_JournalEntryLines_BaseAmount",
                            "(" +
                            "[BaseDebitAmount] > 0 AND " +
                            "[BaseCreditAmount] = 0" +
                            ") OR (" +
                            "[BaseCreditAmount] > 0 AND " +
                            "[BaseDebitAmount] = 0" +
                            ")");
                    });

                entity.HasKey(
                    line => line.Id);

                entity.Property(
                        line => line.Description)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(
                        line => line.DebitAmount)
                    .HasPrecision(19, 4);

                entity.Property(
                        line => line.CreditAmount)
                    .HasPrecision(19, 4);

                entity.Property(
                        line => line.BaseDebitAmount)
                    .HasPrecision(19, 4);

                entity.Property(
                        line => line.BaseCreditAmount)
                    .HasPrecision(19, 4);

                entity.Property(
                        line => line.CostCenter)
                    .HasMaxLength(100);

                entity.Property(
                        line => line.CounterpartyType)
                    .HasMaxLength(50);

                entity.Property(
                        line => line.CounterpartyId)
                    .HasMaxLength(100);

                entity.HasIndex(line => new
                {
                    line.JournalEntryId,
                    line.LineNumber
                })
                    .IsUnique();

                entity.HasIndex(line => new
                {
                    line.CompanyId,
                    line.AccountId
                });

                entity.HasOne(
                        line => line.JournalEntry)
                    .WithMany(
                        entry => entry.Lines)
                    .HasForeignKey(line => new
                    {
                        line.JournalEntryId,
                        line.CompanyId
                    })
                    .HasPrincipalKey(entry => new
                    {
                        entry.Id,
                        entry.CompanyId
                    })
                    .OnDelete(
                        DeleteBehavior.Cascade);

                entity.HasOne(
                        line => line.Account)
                    .WithMany()
                    .HasForeignKey(line => new
                    {
                        line.AccountId,
                        line.CompanyId
                    })
                    .HasPrincipalKey(account => new
                    {
                        account.Id,
                        account.CompanyId
                    })
                    .OnDelete(
                        DeleteBehavior.Restrict);
            });
        }
    }
}
