using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Migrations;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;
using SalesInvoiceService.API.Services;

var database = "BranchMoveChecks_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<SalesInvoiceDbContext>()
    .UseSqlServer(connection)
    .Options;
await using var legacy = new LegacyBranchContext(options);
var created = false;
var checks = 0;

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }

    checks++;
    Console.WriteLine("PASS: " + message);
}

async Task Apply(SalesInvoiceDbContext context, IReadOnlyList<MigrationOperation> operations)
{
    var generator = context.GetService<IMigrationsSqlGenerator>();
    var commands = generator.Generate(operations, context.Model);
    await using var transaction = await context.Database.BeginTransactionAsync();

    foreach (var command in commands)
    {
        await context.Database.ExecuteSqlRawAsync(command.CommandText);
    }

    await transaction.CommitAsync();
}

try
{
    created = await legacy.Database.EnsureCreatedAsync();

    if (!created)
    {
        throw new InvalidOperationException("Refusing to reuse an existing test database.");
    }

    legacy.BranchMasters.AddRange(
        new BranchMaster
        {
            Name = "Office Alpha",
            AccountingCompanyId = 73,
            CompanyName = "Company Alpha",
            CountryCode = "AE",
            Address = "Saved address",
            InvoiceNumberPrefix = "01",
            BankAccountName = "Saved account",
            InvoiceNotes = "Saved notes"
        },
        new BranchMaster
        {
            Name = "Office Beta",
            AccountingCompanyId = 204,
            CountryCode = "IN",
            InvoiceNumberPrefix = "02"
        });
    await legacy.SaveChangesAsync();
    var before = await legacy.BranchMasters.AsNoTracking().OrderBy(branch => branch.Id).ToListAsync();
    var snapshot = JsonSerializer.Serialize(before);
    var objectId = await legacy.Database.SqlQueryRaw<int>(
        "SELECT OBJECT_ID(N'sales.BranchMasters', N'U') AS Value").SingleAsync();

    await using var current = new SalesInvoiceDbContext(options);
    var migration = new MoveBranchesToAmnikon();
    await Apply(current, migration.UpOperations);

    Check(await current.Database.SqlQueryRaw<int>(
        "SELECT OBJECT_ID(N'amnikon.Branches', N'U') AS Value").SingleAsync() == objectId,
        "The original physical table is moved rather than copied or recreated.");
    var after = await current.BranchMasters.AsNoTracking().OrderBy(branch => branch.Id).ToListAsync();
    Check(JsonSerializer.Serialize(after) == snapshot,
        "All values, IDs, company links, invoice prefixes and row versions are preserved.");
    Check(await current.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM sales.BranchMasters").SingleAsync() == 2,
        "The old name resolves to the same rows through a compatibility synonym.");
    Check(await current.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM sys.tables WHERE name IN (N'Branches', N'BranchMasters')").SingleAsync() == 1,
        "Only one physical branch table exists.");

    var service = new BranchService(new BranchRepository(current));
    var profile = await service.GetInvoiceProfileAsync(before[0].Id, CancellationToken.None);
    Check(profile?.InvoiceNumberPrefix == "01" && profile.AccountingCompanyId == 73,
        "Branch invoice details load from the new table without changing their API.");
    await service.UpdateInvoiceProfileAsync(before[0].Id, new UpdateBranchInvoiceProfileDto
    {
        AccountingCompanyId = 73,
        CompanyName = "Updated company",
        InvoiceNumberPrefix = "A1",
        RowVersion = profile!.RowVersion
    }, CancellationToken.None);
    Check(await current.Database.SqlQueryRaw<string>(
        "SELECT CompanyName AS Value FROM sales.BranchMasters WHERE Name=N'Office Alpha'").SingleAsync() == "Updated company",
        "An update through the new model remains visible to older services.");

    await using var stale = new LegacyBranchContext(options);
    var staleBranch = await stale.BranchMasters.SingleAsync(branch => branch.Id == before[0].Id);
    stale.Entry(staleBranch).Property(branch => branch.RowVersion).OriginalValue = before[0].RowVersion;
    staleBranch.CompanyName = "Stale update";

    try
    {
        await stale.SaveChangesAsync();
        throw new InvalidOperationException("A stale row version was accepted.");
    }
    catch (DbUpdateConcurrencyException)
    {
        Check(true, "Concurrency protection still works through the old-name alias.");
    }

    await Apply(current, migration.DownOperations);
    await using var reverted = new LegacyBranchContext(options);
    Check((await reverted.BranchMasters.AsNoTracking().SingleAsync(branch => branch.Id == before[0].Id))
        .InvoiceNumberPrefix == "A1", "Rollback returns the same updated rows to the original table name.");
    Check(await reverted.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM sys.synonyms WHERE name=N'BranchMasters'").SingleAsync() == 0,
        "Rollback removes the compatibility synonym.");
    Console.WriteLine($"{checks} branch migration checks passed.");
}
finally
{
    if (created)
    {
        await legacy.Database.EnsureDeletedAsync();
    }
}

public class LegacyBranchContext : SalesInvoiceDbContext
{
    public LegacyBranchContext(DbContextOptions<SalesInvoiceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<BranchMaster>().ToTable("BranchMasters", "sales");
        modelBuilder.Entity<BranchMaster>().HasIndex(branch => branch.Name)
            .HasDatabaseName("IX_BranchMasters_Name");
    }
}
