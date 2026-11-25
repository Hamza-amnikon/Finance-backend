using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.DTOs;

namespace RoleService.API.Authentication
{
    public sealed class RoleManagementPermissionFilter : IAsyncActionFilter
    {
        private readonly RoleDbContext _context;

        public RoleManagementPermissionFilter(RoleDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (user.IsInRole("Admin"))
            {
                await next();
                return;
            }

            if (!int.TryParse(user.FindFirstValue("role_id"), out var actorRoleId) ||
                actorRoleId <= 0)
            {
                context.Result = new ForbidResult();
                return;
            }

            var action = context.HttpContext.Request.Method switch
            {
                "POST" => "create",
                "PUT" => "edit",
                "DELETE" => "delete",
                _ => string.Empty
            };

            var allowed = await _context.Database.SqlQueryRaw<int>("""
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM security.RolePermissions rp
                    INNER JOIN security.Permissions p ON p.PermissionId = rp.PermissionId
                    WHERE rp.RoleId = {0} AND rp.CompanyId > 0
                        AND p.PermissionCode = N'settings.roles' AND p.Status = N'Active'
                        AND rp.CanView = 1
                        AND (({1} = N'create' AND rp.CanCreate = 1)
                            OR ({1} = N'edit' AND rp.CanEdit = 1)
                            OR ({1} = N'delete' AND rp.CanDelete = 1))
                ) THEN 1 ELSE 0 END AS int) AS Value
                """, actorRoleId, action).SingleAsync(context.HttpContext.RequestAborted);

            if (allowed != 1)
            {
                context.Result = new ForbidResult();
                return;
            }

            var requested = context.ActionArguments.Values
                .OfType<RoleRequest>()
                .FirstOrDefault();

            if (string.Equals(requested?.Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ForbidResult();
                return;
            }

            if (context.ActionArguments.TryGetValue("id", out var value) && value is int targetRoleId)
            {
                var target = await _context.Roles.AsNoTracking()
                    .Where(role => role.RoleId == targetRoleId)
                    .Select(role => role.Role)
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

                if (string.Equals(target, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            await next();
        }
    }
}
