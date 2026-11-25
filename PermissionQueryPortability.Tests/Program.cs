using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using RoleService.API.Repositories;

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var context = new RoleDbContext(new DbContextOptionsBuilder<RoleDbContext>().UseSqlite(connection).Options);
await context.Database.ExecuteSqlRawAsync("""
    CREATE TABLE Permissions (
        PermissionId INTEGER PRIMARY KEY, PermissionCode TEXT, Module TEXT,
        PageName TEXT, Route TEXT, Status TEXT);
    CREATE TABLE RolePermissions (
        PermissionId INTEGER, RoleId INTEGER, CompanyId INTEGER,
        CanView INTEGER DEFAULT 0, CanCreate INTEGER DEFAULT 0, CanEdit INTEGER DEFAULT 0,
        CanDelete INTEGER DEFAULT 0, CanApprove INTEGER DEFAULT 0, CanExport INTEGER DEFAULT 0,
        CanCancel INTEGER DEFAULT 0, CanIssue INTEGER DEFAULT 0, CanPost INTEGER DEFAULT 0,
        CanVoid INTEGER DEFAULT 0, CanReverse INTEGER DEFAULT 0, CanReconcile INTEGER DEFAULT 0);
    INSERT INTO Permissions VALUES
        (1,'sales.clients','Sales','Clients','/sales/clients','Active'),
        (2,'sales.child','Sales','Child','/sales/clients/child','Active'),
        (3,'sales.inactive','Sales','Inactive','/sales/inactive','Inactive');
    INSERT INTO RolePermissions (PermissionId,RoleId,CompanyId,CanView,CanCreate,CanEdit,CanPost) VALUES
        (1,10,1,1,1,0,1), (1,10,2,1,0,1,0), (1,20,3,1,1,1,1),
        (1,10,0,1,1,1,1), (2,10,1,0,1,1,1), (3,10,1,1,1,1,1);
    """);
var rows = await new UserPagePermissionRepository(context).GetByRoleIdAsync(10, CancellationToken.None);
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAIL " + message);
    }
    checks++;
    Console.WriteLine("PASS " + message);
}
Check(rows.Count == 4, "Denied child and inactive routes remain present alongside two company grants");
var first = rows.Single(row => row.Route == "/sales/clients" && row.CompanyId == 1);
var second = rows.Single(row => row.Route == "/sales/clients" && row.CompanyId == 2);
Check(first.CanView && first.CanCreate && first.CanPost && !first.CanEdit, "First company's independent actions are preserved");
Check(second.CanView && second.CanEdit && !second.CanCreate && !second.CanPost, "Second company's independent actions are preserved");
Check(rows.All(row => row.CompanyId is null or 1 or 2), "Other roles and invalid company IDs do not grant access");
Check(rows.Where(row => row.Route != "/sales/clients").All(row => !row.CanView && !row.CanCreate && row.CompanyId == null),
    "View-denied and inactive permissions cannot gain actions");
await OfflineAuthorizationChecks.RunAsync(connection, Check);
Console.WriteLine($"{checks} permission-query and authorization checks passed using an isolated in-memory database.");
