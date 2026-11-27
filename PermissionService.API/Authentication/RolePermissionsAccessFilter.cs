using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PermissionService.API.Data;
using PermissionService.API.DTOs;
using PermissionService.API.Models;

namespace PermissionService.API.Authentication
{
    public sealed class RolePermissionsAccessFilter : IAsyncActionFilter
    {
        private readonly PermissionDbContext _context;
        private readonly ILogger<RolePermissionsAccessFilter> _logger;

        public RolePermissionsAccessFilter(
            PermissionDbContext context,
            ILogger<RolePermissionsAccessFilter> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";

            if (http.User.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var roleName = http.User.FindFirstValue(ClaimTypes.Role);
            var request = context.ActionArguments.Values
                .OfType<SaveRolePermissionsRequest>()
                .FirstOrDefault();
            var companyId = request?.CompanyId ??
                (context.ActionArguments.TryGetValue("companyId", out var value) && value is int selectedCompanyId
                    ? selectedCompanyId
                    : 0);

            if (string.IsNullOrWhiteSpace(roleName) || companyId <= 0)
            {
                context.Result = Error(403, "You do not have access to this company.");
                return;
            }

            try
            {
                var roleId = await _context.Set<PermissionRoleLookup>().AsNoTracking()
                    .Where(role => role.Role == roleName && role.Status == "Active")
                    .Select(role => role.RoleId)
                    .SingleOrDefaultAsync(http.RequestAborted);

                if (roleId <= 0)
                {
                    context.Result = Error(403, "Your assigned role is not active.");
                    return;
                }

                var management = await _context.RolePermissions.AsNoTracking()
                    .Join(_context.Permissions,
                        assignment => assignment.PermissionId,
                        permission => permission.PermissionId,
                        (assignment, permission) => new { assignment, permission })
                    .Where(row => row.assignment.RoleId == roleId &&
                        row.assignment.CompanyId == companyId &&
                        row.permission.PermissionCode == "settings.role-permissions" &&
                        row.permission.Status == "Active")
                    .Select(row => row.assignment)
                    .SingleOrDefaultAsync(http.RequestAborted);

                if (management?.CanView != true ||
                    (request != null && !management.CanEdit))
                {
                    context.Result = Error(403, "Your role does not have this Role Permissions action.");
                    return;
                }

                if (request != null)
                {
                    var actorAssignments = await _context.RolePermissions.AsNoTracking()
                        .Where(row => row.RoleId == roleId && row.CompanyId == companyId)
                        .ToDictionaryAsync(row => row.PermissionId, http.RequestAborted);

                    var targetAssignments = await _context.RolePermissions.AsNoTracking()
                        .Where(row => row.RoleId == request.RoleId && row.CompanyId == companyId)
                        .ToDictionaryAsync(row => row.PermissionId, http.RequestAborted);

                    if (request.Permissions == null || request.Permissions.Any(grant =>
                    {
                        actorAssignments.TryGetValue(grant.PermissionId, out var actor);
                        targetAssignments.TryGetValue(grant.PermissionId, out var target);

                        return ExceedsActor(grant, target, actor);
                    }))
                    {
                        context.Result = Error(403, "You cannot grant an action your role does not have in this company.");
                        return;
                    }
                }

            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                context.Result = new StatusCodeResult(499);
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Role Permissions access verification failed.");
                context.Result = Error(503, "Access verification is unavailable. Please retry.");
                return;
            }

            await next();
        }

        private static bool ExceedsActor(
            RolePermissionRequest grant,
            RolePermission? target,
            RolePermission? actor)
        {
            return (grant.CanView && target?.CanView != true && actor?.CanView != true) ||
                (grant.CanCreate && target?.CanCreate != true && actor?.CanCreate != true) ||
                (grant.CanEdit && target?.CanEdit != true && actor?.CanEdit != true) ||
                (grant.CanDelete && target?.CanDelete != true && actor?.CanDelete != true) ||
                (grant.CanApprove && target?.CanApprove != true && actor?.CanApprove != true) ||
                (grant.CanExport && target?.CanExport != true && actor?.CanExport != true) ||
                (grant.CanCancel && target?.CanCancel != true && actor?.CanCancel != true) ||
                (grant.CanIssue && target?.CanIssue != true && actor?.CanIssue != true) ||
                (grant.CanPost && target?.CanPost != true && actor?.CanPost != true) ||
                (grant.CanVoid && target?.CanVoid != true && actor?.CanVoid != true) ||
                (grant.CanReverse && target?.CanReverse != true && actor?.CanReverse != true) ||
                (grant.CanReconcile && target?.CanReconcile != true && actor?.CanReconcile != true);
        }

        private static ObjectResult Error(int status, string message)
        {
            return new ObjectResult(new { message })
            {
                StatusCode = status
            };
        }
    }
}
