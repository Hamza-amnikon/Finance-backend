using Microsoft.EntityFrameworkCore;
using RoleService.API.Models;
using System.Security.Cryptography;

namespace RoleService.API.Data
{
    public class RoleDbContext : DbContext
    {
        private readonly bool _allowMySqlUserWrites;

        public RoleDbContext(
            DbContextOptions<RoleDbContext> options)
            : base(options)
        {
        }

        public RoleDbContext(
            DbContextOptions<RoleDbContext> options,
            IConfiguration configuration) : this(options)
        {
            _allowMySqlUserWrites = configuration.GetValue<bool>("Database:AllowMySqlUserWrites");
        }

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

            modelBuilder.Entity<Roles>(entity =>
            {
                if (IsMySql)
                {
                    entity.ToTable("security__Roles");
                    entity.Property(role => role.RowVersion).HasColumnType("binary(8)");
                }
                else
                {
                    entity.ToTable("Roles", "security");
                }

                entity.HasIndex(role => role.Role)
                    .IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                if (IsMySql)
                {
                    entity.ToTable("security__Users");
                    entity.Property(user => user.UserId).HasColumnType("char(36)");
                    entity.Property(user => user.TenantId).HasColumnType("char(36)");
                    entity.Property(user => user.ObjectId).HasColumnType("char(36)");
                    entity.Property(user => user.RowVersion)
                        .HasColumnType("binary(8)").IsConcurrencyToken().ValueGeneratedNever();
                }
                else
                {
                    entity.ToTable("Users", "security");
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
        }
    }
}
