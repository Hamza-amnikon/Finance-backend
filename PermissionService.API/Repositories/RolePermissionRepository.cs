using Microsoft.EntityFrameworkCore;
using PermissionService.API.Data;
using PermissionService.API.Models;

namespace PermissionService.API.Repositories
{
    public class RolePermissionRepository : IRolePermissionRepository
    {
        private readonly PermissionDbContext _context;

        public RolePermissionRepository(
            PermissionDbContext context)
        {
            _context = context;
        }

        public async Task<List<RolePermission>> GetByRoleIdAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            return await _context.RolePermissions
                .AsNoTracking()
                .Where(rolePermission =>
                    rolePermission.RoleId == roleId)
                .OrderBy(rolePermission =>
                    rolePermission.PermissionId)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Permission>> GetPermissionsByIdsAsync(
            List<int> permissionIds,
            CancellationToken cancellationToken)
        {
            return await _context.Permissions
                .AsNoTracking()
                .Where(permission =>
                    permissionIds.Contains(permission.PermissionId))
                .OrderBy(permission =>
                    permission.PermissionId)
                .ToListAsync(cancellationToken);
        }

        public async Task SaveAsync(
            int roleId,
            List<RolePermission> rolePermissions,
            CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                throw new ArgumentException(
                    "Select a valid role.",
                    nameof(roleId));
            }

            if (rolePermissions == null)
            {
                throw new ArgumentNullException(
                    nameof(rolePermissions));
            }

            var requestedPermissionIds = new HashSet<int>();

            foreach (var rolePermission in rolePermissions)
            {
                if (rolePermission.RoleId != roleId)
                {
                    throw new ArgumentException(
                        "Every permission assignment must belong to the selected role.",
                        nameof(rolePermissions));
                }

                if (rolePermission.PermissionId <= 0)
                {
                    throw new ArgumentException(
                        "Select a valid permission.",
                        nameof(rolePermissions));
                }

                if (!requestedPermissionIds.Add(
                    rolePermission.PermissionId))
                {
                    throw new ArgumentException(
                        "The same permission cannot be assigned more than once.",
                        nameof(rolePermissions));
                }
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            var existingAssignments = await _context.RolePermissions
                .Where(rolePermission =>
                    rolePermission.RoleId == roleId)
                .ToListAsync(cancellationToken);

            var existingAssignmentsByPermissionId =
                existingAssignments.ToDictionary(
                    rolePermission => rolePermission.PermissionId);

            foreach (var existingAssignment in existingAssignments)
            {
                if (!requestedPermissionIds.Contains(
                    existingAssignment.PermissionId))
                {
                    _context.RolePermissions.Remove(
                        existingAssignment);
                }
            }

            var savedAtUtc = DateTime.UtcNow;

            foreach (var requestedAssignment in rolePermissions)
            {
                if (existingAssignmentsByPermissionId.TryGetValue(
                    requestedAssignment.PermissionId,
                    out var existingAssignment))
                {
                    if (HasChanges(
                        existingAssignment,
                        requestedAssignment))
                    {
                        CopyActions(
                            existingAssignment,
                            requestedAssignment);

                        existingAssignment.ModifiedDate = savedAtUtc;
                    }
                }
                else
                {
                    var newAssignment = new RolePermission
                    {
                        RoleId = roleId,
                        PermissionId = requestedAssignment.PermissionId,
                        CreatedDate = savedAtUtc
                    };

                    CopyActions(
                        newAssignment,
                        requestedAssignment);

                    _context.RolePermissions.Add(
                        newAssignment);
                }
            }

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }

        private static bool HasChanges(
            RolePermission existingAssignment,
            RolePermission requestedAssignment)
        {
            return
                existingAssignment.CanView != requestedAssignment.CanView ||
                existingAssignment.CanCreate != requestedAssignment.CanCreate ||
                existingAssignment.CanEdit != requestedAssignment.CanEdit ||
                existingAssignment.CanDelete != requestedAssignment.CanDelete ||
                existingAssignment.CanApprove != requestedAssignment.CanApprove ||
                existingAssignment.CanExport != requestedAssignment.CanExport ||
                existingAssignment.CanIssue != requestedAssignment.CanIssue ||
                existingAssignment.CanPost != requestedAssignment.CanPost ||
                existingAssignment.CanVoid != requestedAssignment.CanVoid ||
                existingAssignment.CanReverse != requestedAssignment.CanReverse ||
                existingAssignment.CanReconcile != requestedAssignment.CanReconcile;
        }

        private static void CopyActions(
            RolePermission target,
            RolePermission source)
        {
            target.CanView = source.CanView;
            target.CanCreate = source.CanCreate;
            target.CanEdit = source.CanEdit;
            target.CanDelete = source.CanDelete;
            target.CanApprove = source.CanApprove;
            target.CanExport = source.CanExport;
            target.CanIssue = source.CanIssue;
            target.CanPost = source.CanPost;
            target.CanVoid = source.CanVoid;
            target.CanReverse = source.CanReverse;
            target.CanReconcile = source.CanReconcile;
        }
    }
}