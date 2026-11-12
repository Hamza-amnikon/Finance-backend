using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Controllers;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Models;

var database = "OrganizationDeletionTests_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<SalesInvoiceDbContext>()
    .UseSqlServer(connection)
    .Options;
await using var context = new SalesInvoiceDbContext(options);
var created = false;

try
{
    created = await context.Database.EnsureCreatedAsync();

    if (!created)
    {
        throw new InvalidOperationException("Refusing to reuse an existing database.");
    }

    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE sales.OrganizationProfiles
        (
            Id int IDENTITY PRIMARY KEY,
            Name nvarchar(200) NOT NULL,
            Email nvarchar(254) NOT NULL,
            Phone nvarchar(25) NOT NULL,
            Currency nvarchar(3) NOT NULL,
            Address nvarchar(1000) NOT NULL,
            Notes nvarchar(2000) NOT NULL
        );
        EXEC('CREATE SCHEMA audit');
        CREATE TABLE audit.Events
        (
            Id bigint IDENTITY PRIMARY KEY,
            AtUtc datetimeoffset NOT NULL,
            Service nvarchar(200),
            EntityType nvarchar(200),
            EntityId nvarchar(200),
            Action nvarchar(100),
            Actor nvarchar(200),
            ActorKind nvarchar(100),
            Branch nvarchar(100),
            CompanyId int NULL,
            CorrelationId nvarchar(200),
            BeforeJson nvarchar(max),
            AfterJson nvarchar(max)
        );
        """);

    var deleted = new OrganizationProfile
    {
        Name = "Delete test",
        Email = "delete@example.com",
        Currency = "AED"
    };
    var retained = new OrganizationProfile
    {
        Name = "Keep test",
        Email = "keep@example.com",
        Currency = "INR"
    };
    context.OrganizationProfiles.AddRange(deleted, retained);
    await context.SaveChangesAsync();

    var controller = new OrganizationProfilesController(context);
    var result = await controller.Delete(deleted.Id, CancellationToken.None);
    Check(result is NoContentResult, "Deletion returns 204.");
    Check(!await context.OrganizationProfiles.AnyAsync(row => row.Id == deleted.Id), "The selected profile is removed.");
    Check(await context.OrganizationProfiles.AnyAsync(row => row.Id == retained.Id), "Other organization profiles are preserved.");

    var events = await context.Database.SqlQueryRaw<int>("""
        SELECT COUNT(*) AS Value
        FROM audit.Events
        WHERE EntityType = 'OrganizationProfile' AND Action = 'Deleted';
        """).ToListAsync();
    Check(events.Single() == 1, "Deletion is recorded once in audit history.");

    result = await controller.Delete(deleted.Id, CancellationToken.None);
    Check(result is NotFoundObjectResult, "Repeated deletion returns 404.");

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    result = await controller.Delete(retained.Id, cancelled.Token);
    Check(result is StatusCodeResult { StatusCode: 499 }, "Cancelled deletion is handled.");
    Check(await context.OrganizationProfiles.AnyAsync(row => row.Id == retained.Id), "A cancelled request preserves the profile.");
}
finally
{
    if (created)
    {
        await context.Database.EnsureDeletedAsync();
    }
}

static void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException("FAIL: " + description);
    }

    Console.WriteLine("PASS: " + description);
}
