using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.Models;

namespace RoleService.API.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly RoleDbContext _context;

        public UserRepository(
            RoleDbContext context)
        {
            _context = context;
        }

        public Task<List<User>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return _context.Users
                .AsNoTracking()
                .OrderBy(user => user.Name)
                .ThenBy(user => user.UserId)
                .ToListAsync(cancellationToken);
        }

        public Task<User?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return _context.Users
                .SingleOrDefaultAsync(
                    user => user.UserId == userId,
                    cancellationToken);
        }

        public Task<User?> GetByMicrosoftIdentityAsync(
            Guid tenantId,
            Guid objectId,
            CancellationToken cancellationToken)
        {
            return _context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user =>
                        user.TenantId == tenantId &&
                        user.ObjectId == objectId,
                    cancellationToken);
        }

        public void Add(User user)
        {
            _context.Users.Add(user);
        }

        public void SetOriginalRowVersion(
            User user,
            byte[] rowVersion)
        {
            _context.Entry(user)
                .Property(item => item.RowVersion)
                .OriginalValue = rowVersion;
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
