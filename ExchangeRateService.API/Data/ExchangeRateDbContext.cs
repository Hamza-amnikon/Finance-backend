using Microsoft.EntityFrameworkCore;
using ExchangeRateService.API.Models;

namespace ExchangeRateService.API.Data
{
    public class ExchangeRateDbContext : DbContext
    {
        public ExchangeRateDbContext(
            DbContextOptions<ExchangeRateDbContext> options)
            : base(options)
        {
        }

        public DbSet<DailyExchangeRate> DailyExchangeRates => Set<DailyExchangeRate>();
        public DbSet<ExchangeRateCheck> ExchangeRateChecks => Set<ExchangeRateCheck>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<DailyExchangeRate>(entity =>
            {
                entity.ToTable("DailyExchangeRates", "finance");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Currency).HasMaxLength(3);
                entity.Property(row => row.Source).HasMaxLength(80);
                entity.Property(row => row.RateToInr).HasPrecision(28, 12);
                entity.HasIndex(row => new { row.CheckDate, row.Currency }).IsUnique();
            });
            builder.Entity<ExchangeRateCheck>(entity =>
            {
                entity.ToTable("ExchangeRateChecks", "finance");
                entity.HasKey(row => row.CheckDate);
                entity.Property(row => row.Status).HasMaxLength(20);
                entity.Property(row => row.Error).HasMaxLength(1000);
            });
        }
    }
}
