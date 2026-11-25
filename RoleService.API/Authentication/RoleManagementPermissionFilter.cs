using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.Models;
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

            var allowed = await (
                from grant in _context.Set<CompanyPageGrantLookup>().AsNoTracking()
                join permission in _context.Set<PagePermissionLookup>().AsNoTracking()
                    on grant.PermissionId equals permission.PermissionId
                where grant.RoleId == actorRoleId && grant.CompanyId > 0 && grant.CanView &&
                    permission.PermissionCode == "settings.roles" && permission.Status == "Active" &&
                    ((action == "create" && grant.CanCreate) || (action == "edit" && grant.CanEdit) || (action == "delete" && grant.CanDelete))
                select grant.PermissionId
            ).AnyAsync(context.HttpContext.RequestAborted);

            if (!allowed)
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
