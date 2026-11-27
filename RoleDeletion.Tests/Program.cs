using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using RoleService.API.Data;
using RoleService.API.Controllers;
using RoleService.API.Models;
using RoleService.API.Repositories;
using RoleService.API.Services;
using RoleServiceImplementation = RoleService.API.Services.RoleService;

var repository = new TestRoleRepository
{
    SaveFailure = new DbUpdateException(
        "The role is referenced by a user.",
        CreateSqlException(547))
};
var controller = new RoleController(new RoleServiceImplementation(repository));
var response = await controller.DeleteRole(7, default);

Check(response is ConflictObjectResult { StatusCode: 409 },
    "An assigned role returns HTTP 409 when the foreign key blocks deletion.");
Check(JsonSerializer.Serialize(((ConflictObjectResult)response).Value)
        .Contains("Reassign users", StringComparison.Ordinal),
    "The response explains how to resolve the conflict.");

repository = new TestRoleRepository();
controller = new RoleController(new RoleServiceImplementation(repository));
response = await controller.DeleteRole(7, default);
Check(response is NoContentResult && repository.Removed && repository.SaveCalls == 1,
    "An unassigned role retains the existing HTTP 204 deletion behaviour.");

repository = new TestRoleRepository
{
    RoleExists = false
};
controller = new RoleController(new RoleServiceImplementation(repository));
response = await controller.DeleteRole(7, default);
Check(response is NotFoundResult && !repository.Removed && repository.SaveCalls == 0,
    "A missing role retains HTTP 404 without attempting a delete.");

repository = new TestRoleRepository
{
    SaveFailure = new DbUpdateConcurrencyException("The role changed.")
};
controller = new RoleController(new RoleServiceImplementation(repository));
response = await controller.DeleteRole(7, default);
Check(response is ConflictObjectResult { StatusCode: 409 } &&
    JsonSerializer.Serialize(((ConflictObjectResult)response).Value)
        .Contains("Reload", StringComparison.Ordinal),
    "Existing concurrency conflict handling is preserved.");

var unrelatedFailure = new DbUpdateException(
    "An unrelated database failure.",
    CreateSqlException(50000));
repository = new TestRoleRepository
{
    SaveFailure = unrelatedFailure
};

try
{
    await new RoleServiceImplementation(repository).DeleteAsync(7, default);
    throw new Exception("An unrelated database error was swallowed.");
}
catch (DbUpdateException exception) when (ReferenceEquals(exception, unrelatedFailure))
{
    Console.WriteLine("PASS: Unrelated database failures are not reported as assigned roles.");
}

using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
repository = new TestRoleRepository();

try
{
    await new RoleServiceImplementation(repository).DeleteAsync(7, cancellation.Token);
    throw new Exception("Cancellation was ignored.");
}
catch (OperationCanceledException)
{
    Check(!repository.Removed && repository.SaveCalls == 0,
        "Cancellation prevents deletion.");
}

var mysqlConnection = "Server=unused;Database=TestOnly;User ID=test;Password=test;";
var disabledOptions = new DbContextOptionsBuilder<RoleDbContext>().UseMySQL(mysqlConnection).Options;
await using (var disabled = new RoleDbContext(disabledOptions))
{
    disabled.Roles.Add(new Roles { Role = "Test role" });
    try
    {
        await disabled.SaveChangesAsync();
        throw new Exception("A MySQL role write was allowed by default.");
    }
    catch (NotSupportedException)
    {
        Check(true, "MySQL role creation stays disabled by default.");
    }
}

var enabledConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Database:AllowMySqlWrites"] = "true"
}).Build();
var enabledOptions = new DbContextOptionsBuilder<RoleDbContext>()
    .UseMySQL(mysqlConnection)
    .AddInterceptors(new SuppressRoleWrites())
    .Options;
await using (var enabled = new RoleDbContext(enabledOptions, enabledConfiguration))
{
    var newRole = new Roles { Role = "Test role" };
    enabled.Roles.Add(newRole);
    await enabled.SaveChangesAsync();
    Check(newRole.RowVersion.Length == 8,
        "Opt-in MySQL role creation sets a managed concurrency token.");
}

Console.WriteLine("All role deletion and MySQL opt-in checks passed. No database connections were opened.");

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }

    Console.WriteLine("PASS: " + message);
}

static SqlException CreateSqlException(int number)
{
    // SqlClient exposes no public constructors for server errors.
    // Construct the provider error in memory so these tests never need a database.
    var constructor = typeof(SqlError)
        .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(candidate => candidate.GetParameters()[0].ParameterType == typeof(int))
        .OrderBy(candidate => candidate.GetParameters().Length)
        .First();
    var arguments = constructor.GetParameters()
        .Select(parameter => parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : parameter.ParameterType == typeof(string) ? "Test" : null)
        .ToArray();
    arguments[0] = number;

    var error = (SqlError)constructor.Invoke(arguments);
    var errors = (SqlErrorCollection)Activator.CreateInstance(
        typeof(SqlErrorCollection),
        nonPublic: true)!;
    typeof(SqlErrorCollection)
        .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(errors, [error]);

    var factory = typeof(SqlException).GetMethod(
        "CreateException",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        types: [typeof(SqlErrorCollection), typeof(string)],
        modifiers: null)!;

    return (SqlException)factory.Invoke(null, [errors, "Test"])!;
}

sealed class TestRoleRepository : IRoleRepository
{
    public bool RoleExists { get; init; } = true;

    public Exception? SaveFailure { get; init; }

    public bool Removed { get; private set; }

    public int SaveCalls { get; private set; }

    public Task<Roles?> GetByIdAsync(int roleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(RoleExists
            ? new Roles { RoleId = roleId, Role = "Test role" }
            : null);
    }

    public void Remove(Roles role)
    {
        Removed = true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveCalls++;

        return SaveFailure == null
            ? Task.CompletedTask
            : Task.FromException(SaveFailure);
    }

    public Task<List<Roles>> GetAllAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<List<Roles>> GetActiveAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void Add(Roles role)
    {
        throw new NotSupportedException();
    }

    public void SetOriginalRowVersion(Roles role, byte[] rowVersion)
    {
        throw new NotSupportedException();
    }
}

sealed class SuppressRoleWrites : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
}
