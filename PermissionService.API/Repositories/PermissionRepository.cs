using Microsoft.EntityFrameworkCore;
using PermissionService.API.Data;
using PermissionService.API.Models;

namespace PermissionService.API.Repositories
{
    public class PermissionRepository : IPermissionRepository
    {
        private readonly PermissionDbContext _context;

        public PermissionRepository(
            PermissionDbContext context)
        {
            _context = context;
        }

        public async Task<List<Permission>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Permissions
                .AsNoTracking()
                .OrderBy(permission => permission.Module)
                .ThenBy(permission => permission.PageName)
                .ThenBy(permission => permission.PermissionId)
                .ToListAsync(cancellationToken);
        }

        public async Task<Permission?> GetByIdAsync(
            int permissionId,
            CancellationToken cancellationToken)
        {
            return await _context.Permissions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    permission =>
                        permission.PermissionId == permissionId,
                    cancellationToken);
        }

        public async Task<Permission?> GetByCodeAsync(
            string permissionCode,
            CancellationToken cancellationToken)
        {
            return await _context.Permissions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    permission =>
                        permission.PermissionCode == permissionCode,
                    cancellationToken);
        }

        public async Task AddAsync(
            Permission permission,
            CancellationToken cancellationToken)
        {
            _context.Permissions.Add(permission);

            await _context.SaveChangesAsync(
                cancellationToken);
        }

        public async Task UpdateAsync(
            Permission permission,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            _context.Permissions.Update(permission);

            _context.Entry(permission)
                .Property(value => value.RowVersion)
                .OriginalValue = rowVersion;

            await _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}