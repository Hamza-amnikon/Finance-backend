using Microsoft.EntityFrameworkCore;
using RoleService.API.Models;

namespace RoleService.API.Data
{
    public class RoleDbContext : DbContext
    {
        public RoleDbContext(
            DbContextOptions<RoleDbContext> options)
            : base(options)
        {
        }

        public DbSet<Roles> Roles => Set<Roles>();

        public DbSet<User> Users => Set<User>();

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
                entity.ToTable("Roles", "security");

                entity.HasIndex(role => role.Role)
                    .IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users", "security");

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
