using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using SparkFinance.Database;

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
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:MySqlConnection"] = "Server=unused;Port=3306;Database=TestOnly;User ID=test;Password=test;"
}).Build();
var options = new DbContextOptionsBuilder<PermissionContext>();
options.UseMeritMySql(configuration);
var suppress = new NoDatabaseWrites();
options.AddInterceptors(suppress);
await using var context = new PermissionContext(options.Options);
Check(context.Database.ProviderName == "MySql.EntityFrameworkCore", "MySQL provider loads without contacting the database");
var sql = context.Permissions.Where(row => row.CompanyId == 7 && row.CanView).ToQueryString();
Check(sql.Contains("security_Permissions") && !sql.Contains("[security]"), "Permission queries target the explicitly mapped MySQL table");
Check(sql.Contains("CompanyId") && sql.Contains("CanView"), "Company and permission predicates remain in generated SQL");
var row = new PermissionRow { Id = 1, CompanyId = 7, CanView = true, RowVersion = new byte[8] };
context.Attach(row);
row.CanView = false;
await context.SaveChangesAsync();
Check(suppress.OldVersion?.All(value => value == 0) == true, "Updating a row preserves its original concurrency token");
Check(suppress.NewVersion is { Length: 8 } && !suppress.NewVersion.SequenceEqual(suppress.OldVersion!), "Updating a row creates a new eight-byte token");
var property = context.Model.FindEntityType(typeof(PermissionRow))!.FindProperty(nameof(PermissionRow.RowVersion))!;
Check(property.IsConcurrencyToken && property.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
    "MySQL concurrency uses application tokens and preserves optimistic locking");
var missingConfiguration = new ConfigurationBuilder().Build();
try
{
    new DbContextOptionsBuilder().UseMeritMySql(missingConfiguration);
    throw new Exception("Missing MySQL configuration was accepted.");
}
catch (InvalidOperationException exception) when (exception.Message.Contains("MySqlConnection"))
{
    Check(true, "Missing configuration fails explicitly instead of selecting SQL Server");
}
var model = new ModelBuilder();
model.Entity<PermissionRow>().ToTable("Permissions", "security");
try
{
    model.ApplyVerifiedTableMappings(new Dictionary<string, string>());
    throw new Exception("Unverified table mapping was accepted.");
}
catch (InvalidOperationException exception) when (exception.Message.Contains("verified"))
{
    Check(true, "Unverified table names cannot be activated");
}
Console.WriteLine($"{checks} MySQL compatibility checks passed without database writes.");

sealed class PermissionRow
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public bool CanView { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

sealed class PermissionContext(DbContextOptions<PermissionContext> options) : DbContext(options)
{
    public DbSet<PermissionRow> Permissions => Set<PermissionRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PermissionRow>().ToTable("Permissions", "security");
        model.Entity<PermissionRow>().Property(row => row.RowVersion).IsRowVersion();
        model.ApplyVerifiedTableMappings(new Dictionary<string, string>
        {
            ["security.Permissions"] = "security_Permissions"
        });
    }
}

sealed class NoDatabaseWrites : SaveChangesInterceptor
{
    public byte[]? OldVersion { get; private set; }
    public byte[]? NewVersion { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var property = eventData.Context!.Entry(eventData.Context.Set<PermissionRow>().Local.Single())
            .Property(row => row.RowVersion);
        OldVersion = property.OriginalValue.ToArray();
        NewVersion = property.CurrentValue.ToArray();
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
    }
}
