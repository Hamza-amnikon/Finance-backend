using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.Models;

namespace RoleService.API.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly RoleDbContext _context;

        public RoleRepository(RoleDbContext context)
        {
            _context = context;
        }

        public Task<List<Roles>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return _context.Roles
                .AsNoTracking()
                .OrderBy(role => role.Role)
                .ToListAsync(cancellationToken);
        }

        public Task<List<Roles>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return _context.Roles
                .AsNoTracking()
                .Where(role => role.Status == RoleStatus.Active)
                .OrderBy(role => role.Role)
                .ToListAsync(cancellationToken);
        }

        public Task<Roles?> GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            return _context.Roles
                .SingleOrDefaultAsync(
                    role => role.RoleId == roleId,
                    cancellationToken);
        }

        public void Add(Roles role)
        {
            _context.Roles.Add(role);
        }

        public void SetOriginalRowVersion(
            Roles role,
            byte[] rowVersion)
        {
            _context.Entry(role)
                .Property(item => item.RowVersion)
                .OriginalValue = rowVersion;
        }

        public void Remove(Roles role)
        {
            _context.Roles.Remove(role);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
