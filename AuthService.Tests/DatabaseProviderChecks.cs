using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.Models;
using SparkFinance.Database;

internal static class DatabaseProviderChecks
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoleDbContext>();
        Check(context.Database.ProviderName == "MySql.EntityFrameworkCore" &&
            context.Database.GetDbConnection() is MySqlConnection,
            "AuthService resolves the MySQL provider and accepts Port=3306 without opening a connection.");

        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var query = context.Users.AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.ObjectId == objectId)
            .ToQueryString();
        Check(query.Contains("`Users`") && query.Contains("TenantId") && query.Contains("ObjectId") &&
            !query.Contains("[security]"),
            "SSO identity queries use the configured MySQL table and retain tenant and object identity checks.");

        var grantQuery = context.Set<CompanyPageGrantLookup>()
            .Where(grant => grant.RoleId == 3 && grant.CompanyId == 7 && grant.CanView && grant.CanEdit)
            .ToQueryString();
        Check(grantQuery.Contains("`RolePermissions`") && grantQuery.Contains("RoleId") &&
            grantQuery.Contains("CompanyId") && grantQuery.Contains("CanView") && grantQuery.Contains("CanEdit"),
            "MySQL permission queries retain role, company, View and Edit predicates.");

        foreach (var entityType in new[] { typeof(User), typeof(Roles) })
        {
            var version = context.Model.FindEntityType(entityType)!.FindProperty("RowVersion")!;
            Check(version.IsConcurrencyToken && version.ValueGenerated == ValueGenerated.Never &&
                version.GetColumnType() == "binary(8)",
                entityType.Name + " retains optimistic concurrency with MySQL row-version tokens.");
        }

        var alternateConfiguration = Configuration("ArchivedUsers");
        var alternateOptions = new DbContextOptionsBuilder<RoleDbContext>();
        alternateOptions.UseMeritMySql(alternateConfiguration);
        using var alternate = new RoleDbContext(alternateOptions.Options, alternateConfiguration);
        Check(alternate.Users.ToQueryString().Contains("`ArchivedUsers`"),
            "Different configured mappings cannot reuse a cached model for another physical table.");

        var missingConfiguration = Configuration("Users");
        missingConfiguration["Database:MySql:TableMappings:security.Users"] = null;
        var missingOptions = new DbContextOptionsBuilder<RoleDbContext>();
        missingOptions.UseMeritMySql(missingConfiguration);
        using var missing = new RoleDbContext(missingOptions.Options, missingConfiguration);
        try
        {
            _ = missing.Model;
            throw new Exception("An unconfigured identity table mapping was accepted.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("security.Users"))
        {
            Check(true, "Missing table mappings fail explicitly instead of silently selecting another table.");
        }

        var failure = DatabaseErrorChecks.Create(1042);
        Check(DatabaseAvailabilityMiddleware.IsDatabaseFailure(new DbUpdateException("Wrapped outage", failure)) &&
            !DatabaseAvailabilityMiddleware.IsDatabaseFailure(new InvalidOperationException("Application error")),
            "MySQL outages are recognized without hiding unrelated application errors.");

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        var middleware = new DatabaseAvailabilityMiddleware(
            request => Task.FromException(failure),
            NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        await middleware.InvokeAsync(http);
        http.Response.Body.Position = 0;
        var response = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Check(http.Response.StatusCode == 503 && response.Contains("database_unavailable") &&
            !http.Response.Headers.ContainsKey("Set-Cookie") && !response.Contains("Isolated database outage"),
            "A MySQL outage returns 503 without granting access, exposing details or clearing the session.");

        Console.WriteLine("MySQL authentication checks completed without database access.");
    }

    private static IConfigurationRoot Configuration(string usersTable)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MySqlConnection"] = "Server=unused;Port=3306;Database=Unused;User ID=test;Password=test;",
            ["Database:MySql:TableMappings:security.Users"] = usersTable,
            ["Database:MySql:TableMappings:security.Roles"] = "Roles",
            ["Database:MySql:TableMappings:security.Permissions"] = "Permissions",
            ["Database:MySql:TableMappings:security.RolePermissions"] = "RolePermissions"
        }).Build();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception("FAIL " + message);
        }

        Console.WriteLine("PASS " + message);
    }
}
