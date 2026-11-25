using SparkFinance.Database;
using Microsoft.EntityFrameworkCore;
using PermissionService.API.Models;
using System.Security.Cryptography;

namespace PermissionService.API.Data
{
    public class PermissionDbContext : DbContext
    {
        private readonly bool _allowMySqlRolePermissionWrites;

        public PermissionDbContext(
            DbContextOptions<PermissionDbContext> options)
            : base(options)
        {
        }

        public PermissionDbContext(
            DbContextOptions<PermissionDbContext> options,
            IConfiguration configuration) : this(options)
        {
            _allowMySqlRolePermissionWrites = configuration.GetValue<bool>(
                "Database:AllowMySqlRolePermissionWrites");
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (Database.IsFinanceMySql())
            {
                if (!_allowMySqlRolePermissionWrites || ChangeTracker.Entries().Any(entry =>
                    entry.State != EntityState.Unchanged && entry.Entity is not RolePermission))
                {
                    throw new NotSupportedException("MySQL writes are limited to role permissions in this deployment.");
                }

                foreach (var entry in ChangeTracker.Entries<RolePermission>()
                    .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
                {
                    var version = entry.Property(permission => permission.RowVersion);
                    version.CurrentValue = RandomNumberGenerator.GetBytes(8);
                    if (entry.State == EntityState.Modified)
                    {
                        version.IsModified = true;
                    }
                }
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public DbSet<Permission> Permissions => Set<Permission>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PermissionRoleLookup>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Roles", "security", table => table.ExcludeFromMigrations());
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("Permissions", "security");

                entity.HasIndex(permission => permission.PermissionCode)
                    .IsUnique();
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("RolePermissions", "security");

                if (Database.IsFinanceMySql())
                {
                    entity.Property(permission => permission.RowVersion)
                        .HasColumnType("binary(8)").IsConcurrencyToken().ValueGeneratedNever();
                }

                entity.HasIndex(rolePermission => new
                {
                    rolePermission.RoleId,
                    rolePermission.CompanyId,
                    rolePermission.PermissionId
                })
                    .IsUnique()
                    .HasFilter(null);
            });

            modelBuilder.MapFinanceMySql(Database);
        }
    }
}
