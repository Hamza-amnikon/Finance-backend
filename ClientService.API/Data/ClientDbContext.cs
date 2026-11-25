using SparkFinance.Auditing;
using ClientService.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ClientService.API.Data
{
    public class ClientDbContext : AuditedDbContext
    {
        public ClientDbContext(
            DbContextOptions<ClientDbContext> options)
            : base(options)
        {
        }

        public DbSet<CountryMaster> CountryMasters =>
            Set<CountryMaster>();

        public DbSet<Client> Clients => Set<Client>();

        private bool IsMySql => Database.ProviderName == "MySql.EntityFrameworkCore";
        private readonly bool _allowMySqlWrites;
        internal override bool UsesMySqlAudit => IsMySql && _allowMySqlWrites;

        public ClientDbContext(DbContextOptions<ClientDbContext> options, IConfiguration configuration) : this(options)
        {
            _allowMySqlWrites = configuration.GetValue<bool>("Database:AllowMySqlClientWrites");
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (IsMySql && !_allowMySqlWrites)
            {
                throw new NotSupportedException("MySQL client writes are disabled in this deployment.");
            }

            if (UsesMySqlAudit)
            {
                foreach (var entry in ChangeTracker.Entries<Client>()
                    .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
                {
                    var version = entry.Property(client => client.RowVersion);
                    version.CurrentValue = RandomNumberGenerator.GetBytes(8);
                    if (entry.State == EntityState.Modified)
                    {
                        version.IsModified = true;
                    }
                }
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable(IsMySql ? "dbo__ClientMaster" : "ClientMaster");

                entity.HasKey(x => x.Id);

                var rowVersion = entity.Property(x => x.RowVersion);
                if (IsMySql)
                {
                    rowVersion.HasColumnType("binary(8)").IsConcurrencyToken().ValueGeneratedNever();
                    entity.Property(x => x.Id).HasColumnType("char(36)");
                }
                else
                {
                    rowVersion.IsRowVersion();
                }
            });

            modelBuilder.Entity<CountryMaster>(entity =>
            {
                entity.ToTable(IsMySql ? "dbo__countrymasters" : "countrymasters");

                entity.HasKey(x => x.CountryId);

                var countryId = entity.Property(x => x.CountryId);
                if (IsMySql)
                {
                    countryId.ValueGeneratedOnAdd();
                }
                else
                {
                    countryId.UseIdentityColumn();
                }

                entity.HasIndex(x => new
                {
                    x.CountryName,
                    x.State,
                    x.City
                });
            });
        }
    }
}
