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
var mysqlLock = FinancialRowLock.BuildCommandText<PermissionRow>(context, nameof(PermissionRow.Id));
Check(mysqlLock == "SELECT * FROM `security_Permissions` WHERE `Id` = {0} FOR UPDATE",
    "MySQL row lock uses the verified table, parameter placeholder and FOR UPDATE");
Check(!mysqlLock.Contains("UPDLOCK") && !mysqlLock.Contains("HOLDLOCK"),
    "MySQL locking does not emit SQL Server table hints");
try
{
    context.Permissions.LockForUpdate(context, nameof(PermissionRow.Id), 1);
    throw new Exception("A financial lock without a transaction was accepted.");
}
catch (InvalidOperationException exception) when (exception.Message.Contains("active database transaction"))
{
    Check(true, "Financial row locks cannot run outside a transaction");
}
try
{
    FinancialRowLock.BuildCommandText<PermissionRow>(context, "Id; DROP TABLE Permissions");
    throw new Exception("Unmapped lock column was accepted.");
}
catch (InvalidOperationException exception) when (exception.Message.Contains("not mapped"))
{
    Check(true, "Lock columns are resolved from model metadata rather than request SQL");
}
await using var sqlServer = new SqlServerLockContext(new DbContextOptionsBuilder<SqlServerLockContext>()
    .UseSqlServer("Server=unused;Database=TestOnly;Integrated Security=True;TrustServerCertificate=True").Options);
var sqlServerLock = FinancialRowLock.BuildCommandText<PermissionRow>(sqlServer, nameof(PermissionRow.Id));
Check(sqlServerLock == "SELECT * FROM [security].[Permissions] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {0}",
    "Existing SQL Server lock and schema behavior are preserved");
var mysqlLookupLock = FinancialRowLock.BuildCommandText<BillReadRow>(context, nameof(BillReadRow.Id));
Check(mysqlLookupLock == "SELECT * FROM `purchases_Bills` WHERE `Id` = {0} FOR UPDATE",
    "MySQL financial lookup locks use explicitly mapped view identifiers");
var lookupSql = context.Set<BillReadRow>().Where(row => row.Id == 1).ToQueryString();
Check(lookupSql.Contains("`purchases_Bills`") && !lookupSql.Contains("[purchases]"),
    "MySQL keyless lookup queries preserve their configured physical source");
var composedLock = context.Permissions.FromSqlRaw(mysqlLock, 7)
    .Include(row => row.Items).Take(2).ToQueryString();
Check(composedLock.Contains("FOR UPDATE") && composedLock.Contains("LEFT JOIN") &&
    composedLock.Contains("security_PermissionItems"),
    "MySQL row locks remain composable with the item includes used by financial repositories");
DatabaseErrorChecks.Run(Check);
Console.WriteLine($"{checks} MySQL compatibility checks passed without database writes.");

sealed class PermissionRow
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public bool CanView { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<PermissionItemRow> Items { get; set; } = [];
}

sealed class PermissionContext(DbContextOptions<PermissionContext> options) : DbContext(options)
{
    public DbSet<PermissionRow> Permissions => Set<PermissionRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PermissionRow>().ToTable("Permissions", "security");
        model.Entity<PermissionRow>().Property(row => row.RowVersion).IsRowVersion();
        model.Entity<BillReadRow>().HasNoKey().ToView("Bills", "purchases");
        model.Entity<PermissionItemRow>().ToTable("PermissionItems", "security");
        model.Entity<PermissionRow>().HasMany(row => row.Items).WithOne()
            .HasForeignKey(row => row.PermissionRowId);
        model.ApplyVerifiedTableMappings(new Dictionary<string, string>
        {
            ["security.Permissions"] = "security_Permissions",
            ["purchases.Bills"] = "purchases_Bills",
            ["security.PermissionItems"] = "security_PermissionItems"
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

sealed class SqlServerLockContext(DbContextOptions<SqlServerLockContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PermissionRow>().ToTable("Permissions", "security");
    }
}

sealed class BillReadRow
{
    public int Id { get; set; }
}

sealed class PermissionItemRow
{
    public int Id { get; set; }
    public int PermissionRowId { get; set; }
}
