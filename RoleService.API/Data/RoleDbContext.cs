using Microsoft.EntityFrameworkCore;
using RoleService.API.Models;
using System.Security.Cryptography;
using SparkFinance.Database;

namespace RoleService.API.Data
{
    public class RoleDbContext : DbContext, IMySqlTableMappingContext
    {
        private readonly bool _allowMySqlUserWrites;

        public RoleDbContext(
            DbContextOptions<RoleDbContext> options,
            IConfiguration? configuration = null)
            : base(options)
        {
            _allowMySqlUserWrites = configuration?.GetValue<bool>("Database:AllowMySqlUserWrites") == true;
            MySqlTableMappings = configuration?
                .GetSection("Database:MySql:TableMappings")
                .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }

        public IReadOnlyDictionary<string, string> MySqlTableMappings { get; }

        public DbSet<Roles> Roles => Set<Roles>();

        public DbSet<User> Users => Set<User>();

        internal bool IsMySql => Database.ProviderName == "MySql.EntityFrameworkCore";

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (IsMySql)
            {
                if (!_allowMySqlUserWrites || ChangeTracker.Entries()
                    .Any(entry => entry.State != EntityState.Unchanged &&
                        (entry.Entity is not User || entry.State != EntityState.Modified)))
                {
                    throw new NotSupportedException("MySQL writes are limited to user updates in this deployment.");
                }

                foreach (var entry in ChangeTracker.Entries<User>()
                    .Where(entry => entry.State == EntityState.Modified))
                {
                    var version = entry.Property(user => user.RowVersion);
                    version.CurrentValue = RandomNumberGenerator.GetBytes(8);
                    if (entry.State == EntityState.Modified)
                    {
                        version.IsModified = true;
                    }
                }
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PagePermissionLookup>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Permissions", "security", table => table.ExcludeFromMigrations());
            });
            modelBuilder.Entity<CompanyPageGrantLookup>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("RolePermissions", "security", table => table.ExcludeFromMigrations());
            });

            modelBuilder.Entity<Roles>(entity =>
            {
                if (IsMySql && MySqlTableMappings.Count == 0)
                {
                    entity.ToTable("security__Roles");
                }
                else
                {
                    entity.ToTable("Roles", "security");
                }
                if (IsMySql)
                {
                    entity.Property(role => role.RowVersion)
                        .HasColumnType("binary(8)").IsConcurrencyToken().ValueGeneratedNever();
                }

                entity.HasIndex(role => role.Role)
                    .IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                if (IsMySql && MySqlTableMappings.Count == 0)
                {
                    entity.ToTable("security__Users");
                }
                else
                {
                    entity.ToTable("Users", "security");
                }
                if (IsMySql)
                {
                    entity.Property(user => user.UserId).HasColumnType("char(36)");
                    entity.Property(user => user.TenantId).HasColumnType("char(36)");
                    entity.Property(user => user.ObjectId).HasColumnType("char(36)");
                    entity.Property(user => user.RowVersion)
                        .HasColumnType("binary(8)").IsConcurrencyToken().ValueGeneratedNever();
                }
                entity.HasIndex(user => new
                {
                    user.TenantId,
                    user.ObjectId
                })
                    .IsUnique();

                entity.HasOne<Roles>()
                    .WithMany()
                    .HasForeignKey(user => user.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            if (IsMySql && MySqlTableMappings.Count > 0)
            {
                modelBuilder.ApplyVerifiedTableMappings(MySqlTableMappings);
            }
        }
    }
}
