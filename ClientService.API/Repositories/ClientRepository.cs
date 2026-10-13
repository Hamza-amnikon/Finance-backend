using ClientService.API.Data;
using ClientService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientService.API.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly ClientDbContext _context;

        public ClientRepository(ClientDbContext context)
        {
            _context = context;
        }

        public async Task<(List<Client> Data, int Total)> GetAllAsync(
            int page,
            int pageSize,
            string? search,
            CancellationToken cancellationToken)
        {
            var query = _context.Clients.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();

                query = query.Where(x =>
                    x.Name.Contains(value) ||
                    x.CompanyName.Contains(value) ||
                    (x.Email != null && x.Email.Contains(value)));
            }

            var total = await query.CountAsync(cancellationToken);

            var clients = await query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (clients, total);
        }

        public async Task<Client?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return await _context.Clients
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<Client> CreateAsync(
            Client client,
            CancellationToken cancellationToken)
        {
            _context.Clients.Add(client);

            await _context.SaveChangesAsync(cancellationToken);

            return client;
        }

        public async Task<Client> UpdateAsync(
            Client client,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            _context.Entry(client)
                .Property(x => x.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(cancellationToken);

            return client;
        }
    }
}