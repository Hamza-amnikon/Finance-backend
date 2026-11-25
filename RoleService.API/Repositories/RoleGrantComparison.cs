using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.Models;

namespace RoleService.API.Repositories
{
    public static class RoleGrantComparison
    {
        public static Task<bool> ExceedsActorAsync(
            RoleDbContext context,
            int actorRoleId,
            int targetRoleId,
            CancellationToken cancellationToken)
        {
            var actorGrants = context.Set<CompanyPageGrantLookup>().AsNoTracking()
                .Where(grant => grant.RoleId == actorRoleId);
            return context.Set<CompanyPageGrantLookup>().AsNoTracking()
                .Where(target => target.RoleId == targetRoleId)
                .AnyAsync(target =>
                (target.CanView && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanView)) ||
                (target.CanCreate && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanCreate)) ||
                (target.CanEdit && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanEdit)) ||
                (target.CanDelete && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanDelete)) ||
                (target.CanApprove && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanApprove)) ||
                (target.CanExport && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanExport)) ||
                (target.CanCancel && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanCancel)) ||
                (target.CanIssue && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanIssue)) ||
                (target.CanPost && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanPost)) ||
                (target.CanVoid && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanVoid)) ||
                (target.CanReverse && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanReverse)) ||
                (target.CanReconcile && !actorGrants.Any(actor =>
                    actor.CompanyId == target.CompanyId && actor.PermissionId == target.PermissionId && actor.CanReconcile)),
                    cancellationToken);

        }
    }
}
