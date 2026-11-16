using Microsoft.EntityFrameworkCore;
using PermissionService.API.Models;

namespace PermissionService.API.Data
{
    public class PermissionDbContext : DbContext
    {
        public PermissionDbContext(
            DbContextOptions<PermissionDbContext> options)
            : base(options)
        {
        }

        public DbSet<Permission> Permissions => Set<Permission>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("Permissions", "security");

                entity.HasIndex(permission => permission.PermissionCode)
                    .IsUnique();
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("RolePermissions", "security");

                entity.HasIndex(rolePermission => new
                {
                    rolePermission.RoleId,
                    rolePermission.PermissionId
                })
                    .IsUnique();
            });
        }
    }
}
