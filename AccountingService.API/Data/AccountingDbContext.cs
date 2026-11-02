using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Data
{
    public class AccountingDbContext : DbContext
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

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(
                "accounting");

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
    }
}