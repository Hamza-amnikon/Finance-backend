using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.DTOs;
using RoleService.API.Models;

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
            if (_context.IsMySql)
            {
                return _context.Database.SqlQuery<UserPagePermissionResponse>($"""
                    SELECT p.PermissionCode, p.Module, p.Route, rp.CompanyId,
                        (rp.CompanyId IS NOT NULL) AS CanView,
                        COALESCE(rp.CanCreate, FALSE) AS CanCreate,
                        COALESCE(rp.CanEdit, FALSE) AS CanEdit,
                        COALESCE(rp.CanDelete, FALSE) AS CanDelete,
                        COALESCE(rp.CanApprove, FALSE) AS CanApprove,
                        COALESCE(rp.CanExport, FALSE) AS CanExport,
                        COALESCE(rp.CanCancel, FALSE) AS CanCancel,
                        COALESCE(rp.CanIssue, FALSE) AS CanIssue,
                        COALESCE(rp.CanPost, FALSE) AS CanPost,
                        COALESCE(rp.CanVoid, FALSE) AS CanVoid,
                        COALESCE(rp.CanReverse, FALSE) AS CanReverse,
                        COALESCE(rp.CanReconcile, FALSE) AS CanReconcile
                    FROM security__Permissions p
                    LEFT JOIN security__RolePermissions rp
                        ON rp.PermissionId = p.PermissionId AND rp.RoleId = {roleId}
                        AND rp.CanView = 1 AND rp.CompanyId > 0 AND p.Status = 'Active'
                    ORDER BY p.Module, p.PageName, rp.CompanyId
                    """).ToListAsync(cancellationToken);
            }

            return _context.Database.SqlQuery<UserPagePermissionResponse>($"""
                SELECT p.PermissionCode, p.Module, p.Route, rp.CompanyId,
                    CAST(CASE WHEN rp.CompanyId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS CanView,
                    CAST(COALESCE(rp.CanCreate, 0) AS bit) AS CanCreate,
                    CAST(COALESCE(rp.CanEdit, 0) AS bit) AS CanEdit,
                    CAST(COALESCE(rp.CanDelete, 0) AS bit) AS CanDelete,
                    CAST(COALESCE(rp.CanApprove, 0) AS bit) AS CanApprove,
                    CAST(COALESCE(rp.CanExport, 0) AS bit) AS CanExport,
                    CAST(COALESCE(rp.CanCancel, 0) AS bit) AS CanCancel,
                    CAST(COALESCE(rp.CanIssue, 0) AS bit) AS CanIssue,
                    CAST(COALESCE(rp.CanPost, 0) AS bit) AS CanPost,
                    CAST(COALESCE(rp.CanVoid, 0) AS bit) AS CanVoid,
                    CAST(COALESCE(rp.CanReverse, 0) AS bit) AS CanReverse,
                    CAST(COALESCE(rp.CanReconcile, 0) AS bit) AS CanReconcile
                FROM security.Permissions p
                LEFT JOIN security.RolePermissions rp
                    ON rp.PermissionId = p.PermissionId AND rp.RoleId = {roleId}
                    AND rp.CanView = 1 AND rp.CompanyId > 0 AND p.Status = 'Active'
                ORDER BY p.Module, p.PageName, rp.CompanyId
                """).ToListAsync(cancellationToken);
        }
    }
}
