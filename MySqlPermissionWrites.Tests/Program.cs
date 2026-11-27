using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using PermissionService.API.Data;
using PermissionService.API.Models;

const string connection = "Server=unused;Database=TestOnly;User ID=test;Password=test;";
var disabledOptions = new DbContextOptionsBuilder<PermissionDbContext>().UseMySQL(connection).Options;
await using (var disabled = new PermissionDbContext(disabledOptions))
{
    disabled.Permissions.Add(NewPermission());
    try
    {
        await disabled.SaveChangesAsync();
        throw new Exception("A MySQL permission write was allowed by default.");
    }
    catch (NotSupportedException)
    {
        Console.WriteLine("PASS: MySQL permission writes are disabled by default.");
    }
}

var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Database:AllowMySqlWrites"] = "true"
}).Build();
var enabledOptions = new DbContextOptionsBuilder<PermissionDbContext>()
    .UseMySQL(connection)
    .AddInterceptors(new SuppressWrites())
    .Options;
await using (var enabled = new PermissionDbContext(enabledOptions, configuration))
{
    var permission = NewPermission();
    enabled.Permissions.Add(permission);
    await enabled.SaveChangesAsync();
    if (permission.RowVersion.Length != 8)
    {
        throw new Exception("Permission creation did not set an eight-byte row version.");
    }
    Console.WriteLine("PASS: Opt-in permission creation sets a managed concurrency token.");
}

Console.WriteLine("MySQL permission opt-in checks passed without a database connection.");

static Permission NewPermission() => new()
{
    PermissionCode = "test.page",
    Module = "Test",
    PageName = "Test"
};

sealed class SuppressWrites : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
}
