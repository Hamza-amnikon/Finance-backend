using ExpenseService.API.Models;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Auditing;

namespace ExpenseService.API.Data
{
    // Onboarding uses the existing tables in one transaction; their owning
    // contexts continue to manage all schema migrations and financial records.
    public class CompanySetupDbContext : AuditedDbContext
    {
        public CompanySetupDbContext(
            DbContextOptions<CompanySetupDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies => Set<Company>();

        public DbSet<CompanySetupBranch> Branches => Set<CompanySetupBranch>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Company>(entity =>
            {
                entity.ToTable("Companies", "expenses", table =>
                    table.ExcludeFromMigrations());

                entity.HasIndex(company => company.Name).IsUnique();
            });

            builder.Entity<CompanySetupBranch>(entity =>
            {
                entity.ToTable("Branches", "amnikon", table =>
                    table.ExcludeFromMigrations());

                entity.Property(branch => branch.RowVersion).IsRowVersion();
            });
        }
    }
}
