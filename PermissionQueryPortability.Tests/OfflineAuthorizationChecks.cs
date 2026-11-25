using System.Security.Claims;
using System.Text.Json;
using AccountingService.API.Controllers;
using AccountingService.API.Data;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionService.API.Authentication;
using PermissionService.API.Data;
using PermissionService.API.DTOs;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.DTOs;
using RoleService.API.Repositories;

internal static class OfflineAuthorizationChecks
{
    public static async Task RunAsync(SqliteConnection connection, Action<bool, string> check)
    {
        await using var roles = new RoleDbContext(new DbContextOptionsBuilder<RoleDbContext>().UseSqlite(connection).Options);
        await roles.Database.ExecuteSqlRawAsync("""
            ALTER TABLE RolePermissions ADD COLUMN RolePermissionId INTEGER;
            ALTER TABLE RolePermissions ADD COLUMN CreatedDate TEXT DEFAULT '2026-01-01';
            ALTER TABLE RolePermissions ADD COLUMN ModifiedDate TEXT;
            ALTER TABLE RolePermissions ADD COLUMN RowVersion BLOB DEFAULT X'0000000000000000';
            UPDATE RolePermissions SET RolePermissionId = rowid;
            CREATE TABLE Roles (RoleId INTEGER, Role TEXT, Status TEXT);
            INSERT INTO Roles VALUES (10,'Reviewer','Active'),(20,'Operator','Active'),(99,'Admin','Active');
            INSERT INTO Permissions VALUES
                (4,'settings.users','Settings','Users','/settings/users','Active'),
                (5,'settings.roles','Settings','Roles','/settings/roles','Active'),
                (6,'settings.role-permissions','Settings','Role Permissions','/settings/role-permissions','Active');
            INSERT INTO RolePermissions (RolePermissionId,PermissionId,RoleId,CompanyId,CanView,CanCreate,CanEdit,CanDelete) VALUES
                (10,4,10,1,1,1,0,0),(11,5,10,1,1,1,0,1),(12,6,10,1,1,0,1,0),
                (13,4,30,1,0,0,1,0);
            """);
        check(!await RoleGrantComparison.ExceedsActorAsync(roles, 10, 10, CancellationToken.None),
            "A role's own grants do not exceed itself");
        check(await RoleGrantComparison.ExceedsActorAsync(roles, 10, 20, CancellationToken.None),
            "A target role's grants in another company exceed the actor's scope");
        check(await RoleGrantComparison.ExceedsActorAsync(roles, 10, 30, CancellationToken.None),
            "Delegation comparison retains restrictions on each action");
        await CheckFilter(new UsersPermissionFilter(roles), "GET", 10, "Reviewer", true, check, "Users View permits reading");
        await CheckFilter(new UsersPermissionFilter(roles), "POST", 10, "Reviewer", true, check, "Users Create permits creation independently");
        await CheckFilter(new UsersPermissionFilter(roles), "PUT", 10, "Reviewer", false, check, "Users View and Create do not imply Edit");
        await CheckFilter(new UsersPermissionFilter(roles), "PUT", 30, "Other", false, check, "Users Edit without View is denied");
        await CheckFilter(new UsersPermissionFilter(roles), "POST", 77, "Other", false, check, "Another role cannot inherit Users actions");
        await CheckFilter(new UsersPermissionFilter(roles), "PUT", 0, "Admin", true, check, "Admin behavior is preserved");
        await CheckFilter(new RoleManagementPermissionFilter(roles), "POST", 10, "Reviewer", true, check, "Roles Create is independent of Edit");
        await CheckFilter(new RoleManagementPermissionFilter(roles), "PUT", 10, "Reviewer", false, check, "Roles Edit is denied without its grant");
        await CheckFilter(new RoleManagementPermissionFilter(roles), "DELETE", 10, "Reviewer", true, check, "Roles Delete follows its grant");
        await CheckFilter(new RoleManagementPermissionFilter(roles), "POST", 10, "Reviewer", false, check,
            "Non-Admin cannot create an Admin role", new RoleRequest { Role = "Admin" });

        await using var permissions = new PermissionDbContext(new DbContextOptionsBuilder<PermissionDbContext>().UseSqlite(connection).Options);
        var filter = new RolePermissionsAccessFilter(permissions, NullLogger<RolePermissionsAccessFilter>.Instance);
        await CheckManagement(filter, 20, 1, false, true, check, "Delegated administrator can save actions it holds");
        await CheckManagement(filter, 20, 1, true, false, check, "Delegated administrator cannot grant an action it lacks");
        await CheckManagement(filter, 99, 1, false, false, check, "Admin permission protection is preserved");
        await CheckManagement(filter, 20, 2, false, false, check, "Permission management cannot cross company grants");

        await using var accounting = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseSqlite(connection).Options);
        await accounting.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Branches (Id INTEGER, Name TEXT, IsActive INTEGER, AccountingCompanyId INTEGER);
            INSERT INTO Branches VALUES (1,'Branch A',1,1),(2,'Branch B',1,2),(3,'Duplicate',1,1),(4,'Duplicate',1,2);
            """);
        var fake = new TestPageAccess();
        var controller = new CompanyAccessController(accounting, fake, NullLogger<CompanyAccessController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = await controller.Get("/sales/clients/child/details", CancellationToken.None);
        check(result is OkObjectResult && fake.PermissionCode == "sales.child", "Most specific child permission remains authoritative");
        using (var response = JsonDocument.Parse(JsonSerializer.Serialize(((OkObjectResult)result).Value)))
        {
            check(response.RootElement.GetProperty("branches").GetArrayLength() == 1,
                "Company access excludes duplicate branches and ungranted companies");
        }
        await accounting.Database.ExecuteSqlRawAsync("INSERT INTO Permissions VALUES (7,'sales.child.duplicate','Sales','Duplicate','/sales/clients/child','Active')");
        result = await controller.Get("/sales/clients/child/details", CancellationToken.None);
        check(result is ObjectResult { StatusCode: 403 }, "Ambiguous page permission mapping fails closed");
        result = await controller.Get("/not-configured", CancellationToken.None);
        check(result is ObjectResult { StatusCode: 403 }, "Unknown page routes fail closed");
    }

    private static ActionExecutingContext Context(string method, int roleId, string role, Dictionary<string, object?> arguments)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, role), new Claim("role_id", roleId.ToString())
        ], "OfflineTest"));
        return new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], arguments, new object());
    }

    private static async Task CheckFilter(
        IAsyncActionFilter filter, string method, int roleId, string role, bool expected,
        Action<bool, string> check, string message, RoleRequest? request = null)
    {
        var context = Context(method, roleId, role, request == null ? [] : new() { ["request"] = request });
        var called = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });
        check(called == expected && (called || context.Result is ForbidResult), message);
    }

    private static async Task CheckManagement(
        RolePermissionsAccessFilter filter, int targetRole, int companyId, bool edit, bool expected,
        Action<bool, string> check, string message)
    {
        var context = Context("PUT", 10, "Reviewer", new()
        {
            ["request"] = new SaveRolePermissionsRequest
            {
                RoleId = targetRole, CompanyId = companyId,
                Permissions = [new RolePermissionRequest { PermissionId = 1, CanView = true, CanCreate = true, CanEdit = edit }]
            }
        });
        var called = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });
        check(called == expected && (called || context.Result is ObjectResult { StatusCode: 403 }), message);
    }

    private sealed class TestPageAccess : IAccountingPageAccess
    {
        public string? PermissionCode { get; private set; }
        public Task<LedgerAccess> GetAsync(HttpRequest request, string? permissionCode, CancellationToken cancellationToken)
        {
            PermissionCode = permissionCode;
            return Task.FromResult(new LedgerAccess(200, false, [1]));
        }
    }
}
