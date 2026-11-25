using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.Models;

namespace RoleService.API.Authentication
{
    public sealed class UsersPermissionFilter : IAsyncActionFilter
    {
        private readonly RoleDbContext _context;

        public UsersPermissionFilter(RoleDbContext context)
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

            if (!int.TryParse(user.FindFirstValue("role_id"), out var roleId) || roleId <= 0)
            {
                context.Result = new ForbidResult();
                return;
            }

            var action = context.HttpContext.Request.Method switch
            {
                "GET" => "view",
                "POST" => "create",
                "PUT" => "edit",
                _ => string.Empty
            };

            var allowed = await (
                from grant in _context.Set<CompanyPageGrantLookup>().AsNoTracking()
                join permission in _context.Set<PagePermissionLookup>().AsNoTracking()
                    on grant.PermissionId equals permission.PermissionId
                where grant.RoleId == roleId && grant.CompanyId > 0 && grant.CanView &&
                    permission.PermissionCode == "settings.users" && permission.Status == "Active" &&
                    ((action == "view") || (action == "create" && grant.CanCreate) || (action == "edit" && grant.CanEdit))
                select grant.PermissionId
            ).AnyAsync(context.HttpContext.RequestAborted);

            if (!allowed)
            {
                context.Result = new ForbidResult();
                return;
            }

            await next();
        }
    }
}
