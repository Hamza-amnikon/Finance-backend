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
            var grants = _context.Set<CompanyPageGrantLookup>().AsNoTracking()
                .Where(grant => grant.RoleId == roleId && grant.CanView && grant.CompanyId > 0);

            var query =
                from permission in _context.Set<PagePermissionLookup>().AsNoTracking()
                join grant in grants
                    on new { permission.PermissionId, Active = permission.Status }
                    equals new { grant.PermissionId, Active = "Active" } into matchingGrants
                from grant in matchingGrants.DefaultIfEmpty()
                orderby permission.Module, permission.PageName, grant.CompanyId
                select new UserPagePermissionResponse
                {
                    PermissionCode = permission.PermissionCode,
                    Module = permission.Module,
                    Route = permission.Route,
                    CompanyId = grant.CompanyId,
                    CanView = grant.CompanyId != null,
                    CanCreate = grant.CompanyId != null && grant.CanCreate,
                    CanEdit = grant.CompanyId != null && grant.CanEdit,
                    CanDelete = grant.CompanyId != null && grant.CanDelete,
                    CanApprove = grant.CompanyId != null && grant.CanApprove,
                    CanExport = grant.CompanyId != null && grant.CanExport,
                    CanCancel = grant.CompanyId != null && grant.CanCancel,
                    CanIssue = grant.CompanyId != null && grant.CanIssue,
                    CanPost = grant.CompanyId != null && grant.CanPost,
                    CanVoid = grant.CompanyId != null && grant.CanVoid,
                    CanReverse = grant.CompanyId != null && grant.CanReverse,
                    CanReconcile = grant.CompanyId != null && grant.CanReconcile
                };

            return query.ToListAsync(cancellationToken);
        }
    }
}
