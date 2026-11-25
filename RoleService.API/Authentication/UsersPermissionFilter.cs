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

            var permissionSql = _context.IsMySql ? """
                SELECT EXISTS (
                    SELECT 1 FROM security__RolePermissions rp
                    INNER JOIN security__Permissions p ON p.PermissionId = rp.PermissionId
                    WHERE rp.RoleId = {0} AND rp.CompanyId > 0
                        AND p.PermissionCode = 'settings.users' AND p.Status = 'Active'
                        AND rp.CanView = 1
                        AND (({1} = 'view') OR ({1} = 'create' AND rp.CanCreate = 1)
                            OR ({1} = 'edit' AND rp.CanEdit = 1))
                ) AS Value
                """ : """
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM security.RolePermissions rp
                    INNER JOIN security.Permissions p ON p.PermissionId = rp.PermissionId
                    WHERE rp.RoleId = {0} AND rp.CompanyId > 0
                        AND p.PermissionCode = N'settings.users' AND p.Status = N'Active'
                        AND rp.CanView = 1
                        AND (({1} = N'view') OR ({1} = N'create' AND rp.CanCreate = 1)
                            OR ({1} = N'edit' AND rp.CanEdit = 1))
                ) THEN 1 ELSE 0 END AS int) AS Value
                """;
            var allowed = await _context.Database.SqlQueryRaw<int>(permissionSql, roleId, action)
                .SingleAsync(context.HttpContext.RequestAborted);

            if (allowed == 0)
            {
                context.Result = new ForbidResult();
                return;
            }

            await next();
        }
    }
}
