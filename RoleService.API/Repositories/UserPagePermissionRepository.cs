using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.DTOs;

namespace RoleService.API.Repositories
{
    public sealed class UserPagePermissionRepository : IUserPagePermissionRepository
    {
        private readonly RoleDbContext _context;

        public UserPagePermissionRepository(RoleDbContext context)
        {
            _context = context;
        }

        public Task<List<UserPagePermissionResponse>> GetByRoleIdAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            // Keep denied routes so a permitted parent cannot grant a separately controlled child page.
            return _context.Database.SqlQuery<UserPagePermissionResponse>($"""
                SELECT p.PermissionCode, p.Module, p.Route, rp.CompanyId,
                    CAST(CASE WHEN rp.CompanyId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS CanView,
                    CAST(COALESCE(rp.CanCreate, 0) AS bit) AS CanCreate,
                    CAST(COALESCE(rp.CanEdit, 0) AS bit) AS CanEdit
                FROM security.Permissions p
                LEFT JOIN security.RolePermissions rp
                    ON rp.PermissionId = p.PermissionId AND rp.RoleId = {roleId}
                    AND rp.CanView = 1 AND rp.CompanyId > 0 AND p.Status = 'Active'
                ORDER BY p.Module, p.PageName, rp.CompanyId
                """).ToListAsync(cancellationToken);
        }
    }
}
