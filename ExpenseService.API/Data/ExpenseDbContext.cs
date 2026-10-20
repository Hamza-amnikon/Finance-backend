using ExpenseService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Data
{
    public class ExpenseDbContext : DbContext
    {
        public ExpenseDbContext(
            DbContextOptions<ExpenseDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies => Set<Company>();

        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

        public DbSet<Expense> Expenses => Set<Expense>();

        public DbSet<Receipt> Receipts => Set<Receipt>();

        public DbSet<ExpenseCountryLookup> Countries => Set<ExpenseCountryLookup>();

        public DbSet<ExpenseCurrencyLookup> Currencies => Set<ExpenseCurrencyLookup>();

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            builder.HasDefaultSchema("expenses");

            builder.Entity<ExpenseCountryLookup>()
                .HasNoKey()
                .ToView("countrymasters", "dbo");

            builder.Entity<ExpenseCurrencyLookup>()
                .HasNoKey()
                .ToView("CurrencyMasters", "sales");

            builder.Entity<Company>(entity =>
            {
                entity.ToTable("Companies");

                entity.HasIndex(row => row.Name)
                    .IsUnique();
            });

            builder.Entity<BankAccount>(entity =>
            {
                entity.ToTable("BankAccounts");

                entity.HasIndex(row => new
                {
                    row.CompanyId,
                    row.Name
                })
                    .IsUnique();

                entity.HasAlternateKey(row => new
                {
                    row.Id,
                    row.CompanyId
                });

                entity.HasOne(row => row.Company)
                    .WithMany()
                    .HasForeignKey(row => row.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Expense>(entity =>
            {
                entity.ToTable("Expenses", table =>
                {
                    table.HasCheckConstraint(
                        "CK_Expense_Amount",
                        "[Amount] > 0");

                    table.HasCheckConstraint(
                        "CK_Expense_Rate",
                        "[Rate] > 0 AND [Rate] <= 1000000");

                    table.HasCheckConstraint(
                        "CK_Expense_Currency",
                        "LEN([Currency]) = 3");

                    table.HasCheckConstraint(
                        "CK_Expense_Status",
                        "[Status] IN ('Draft', 'Recorded', 'Voided')");

                    table.HasCheckConstraint(
                        "CK_Expense_Account",
                        "[Status] <> 'Recorded' OR [BankAccountId] IS NOT NULL");
                });

                entity.HasIndex(row => row.RequestId)
                    .IsUnique();

                entity.HasIndex(row => new
                {
                    row.CompanyId,
                    row.Date
                });

                entity.Property(row => row.Amount)
                    .HasPrecision(18, 4);

                entity.Property(row => row.Rate)
                    .HasPrecision(18, 6);

                entity.Property(row => row.RowVersion)
                    .IsRowVersion();

                entity.HasOne(row => row.Company)
                    .WithMany()
                    .HasForeignKey(row => row.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Prevent using another company's account, including direct database writes.
                entity.HasOne(row => row.BankAccount)
                    .WithMany()
                    .HasForeignKey(row => new
                    {
                        row.BankAccountId,
                        row.CompanyId
                    })
                    .HasPrincipalKey(row => new
                    {
                        row.Id,
                        row.CompanyId
                    })
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Receipt>()
                .ToTable("Receipts");

            builder.Entity<Receipt>()
                .HasOne(row => row.Expense)
                .WithOne(row => row.Receipt)
                .HasForeignKey<Receipt>(row => row.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
