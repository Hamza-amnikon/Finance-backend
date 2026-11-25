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
                var roleId = await _context.Database.SqlQuery<int>($"""
                    SELECT RoleId AS Value
                    FROM security.Roles
                    WHERE Role = {roleName} AND Status = N'Active'
                    """).SingleOrDefaultAsync(http.RequestAborted);

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
                    var targetIsAdmin = await _context.Database.SqlQuery<int>($"""
                        SELECT CAST(CASE WHEN EXISTS (
                            SELECT 1 FROM security.Roles
                            WHERE RoleId = {request.RoleId} AND Role = N'Admin'
                        ) THEN 1 ELSE 0 END AS int) AS Value
                        """).SingleAsync(http.RequestAborted);

                    if (targetIsAdmin == 1)
                    {
                        context.Result = Error(403, "Only Admin can change Admin permissions.");
                        return;
                    }

                    var actorAssignments = await _context.RolePermissions.AsNoTracking()
                        .Where(row => row.RoleId == roleId && row.CompanyId == companyId)
                        .ToDictionaryAsync(row => row.PermissionId, http.RequestAborted);

                    if (request.Permissions == null || request.Permissions.Any(grant =>
                        !actorAssignments.TryGetValue(grant.PermissionId, out var actor) ||
                        ExceedsActor(grant, actor)))
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

        private static bool ExceedsActor(RolePermissionRequest grant, RolePermission actor)
        {
            return (grant.CanView && !actor.CanView) ||
                (grant.CanCreate && !actor.CanCreate) ||
                (grant.CanEdit && !actor.CanEdit) ||
                (grant.CanDelete && !actor.CanDelete) ||
                (grant.CanApprove && !actor.CanApprove) ||
                (grant.CanExport && !actor.CanExport) ||
                (grant.CanCancel && !actor.CanCancel) ||
                (grant.CanIssue && !actor.CanIssue) ||
                (grant.CanPost && !actor.CanPost) ||
                (grant.CanVoid && !actor.CanVoid) ||
                (grant.CanReverse && !actor.CanReverse) ||
                (grant.CanReconcile && !actor.CanReconcile);
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
