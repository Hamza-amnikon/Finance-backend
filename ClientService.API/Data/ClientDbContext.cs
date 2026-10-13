using ClientService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientService.API.Data
{
    public class ClientDbContext : DbContext
    {
        public ClientDbContext(
            DbContextOptions<ClientDbContext> options)
            : base(options)
        {
        }

        public DbSet<CountryMaster> CountryMasters =>
            Set<CountryMaster>();

        public DbSet<Client> Clients => Set<Client>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("ClientMaster");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();
            });

            modelBuilder.Entity<CountryMaster>(entity =>
            {
                entity.ToTable("countrymasters");

                entity.HasKey(x => x.CountryId);

                entity.Property(x => x.CountryId)
                    .UseIdentityColumn();

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