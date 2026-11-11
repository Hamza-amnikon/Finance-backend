using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AccountingService.API.Data;
using AccountingService.API.Models;
using ExpenseService.API.Data;
using ExpenseService.API.Models;
using Microsoft.EntityFrameworkCore;

// Never write audit test data to SparkFinance.
var database = "OrbitAuditTests_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var auditOptions = new DbContextOptionsBuilder<AccountingDbContext>().UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__AccountingMigrationsHistory", "accounting")).Options;
await using var context = new AccountingDbContext(auditOptions);
await using var expenses = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__ExpenseMigrationsHistory", "expenses")).Options);
var created = false;
Process? server = null;
var output = new StringBuilder();
var checks = 0;
void Check(
    bool condition,
    string name
)
{
    if (!condition)
        throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    checks++;
}

try
{
    await expenses.Database.MigrateAsync();
    created = true;
    await expenses.Database.ExecuteSqlRawAsync("CREATE SCHEMA sales;");
    await expenses.Database.ExecuteSqlRawAsync("CREATE TABLE sales.SalesInvoices (Id int, InvoiceNumber nvarchar(50), Branch nvarchar(100), Currency nvarchar(3), Status nvarchar(20), GrandTotal decimal(18,2)); INSERT INTO sales.SalesInvoices VALUES (42,'INV-BASELINE','Office','INR','Issued',100);");
    await context.Database.MigrateAsync();
    var baseline = await context.AuditEvents.AsNoTracking().SingleAsync();
    Check(
        baseline.Action == "Snapshot" && baseline.BeforeJson == null && baseline.AfterJson!.Contains("INV-BASELINE"),
        "Existing data is an explicit baseline snapshot, not fake history"
    );
    await expenses.Database.ExecuteSqlRawAsync("CREATE TABLE sales.BranchMasters (Name nvarchar(100), AccountingCompanyId int); INSERT INTO sales.BranchMasters VALUES ('Dubai',1),('Turbhe',2);");
    expenses.Companies.Add(new Company { Name = "Audit company", Country = "IN", Currency = "INR" });
    await expenses.SaveChangesAsync();
    var companyId = expenses.Companies.Single().Id;
    Check(
        await context.AuditEvents.CountAsync(row => row.EntityType == "Company") == 1,
        "Another service writes to the same audit store"
    );
    var account = new Account
    {
        CompanyId = companyId,
        Code = "TEST-1",
        Name = "Before",
        Type = AccountType.Expense,
        NormalBalance = AccountNormalBalance.Debit,
        Description = "Do not capture confidential description"
    };
    context.Accounts.Add(account);
    await context.SaveChangesAsync();
    var added = await context.AuditEvents.AsNoTracking().SingleAsync(row => row.EntityType == "Account");
    Check(
        added.Action == "Created" && added.EntityId == account.Id.ToString() && added.BeforeJson == null,
        "Generated record ID is captured after insert"
    );
    Check(
        !added.AfterJson!.Contains("confidential") && !added.AfterJson.Contains("RowVersion"),
        "Sensitive/free-text fields and row versions are excluded"
    );
    account.Description = "Changed confidential description";
    await context.SaveChangesAsync();
    var excluded = await context.AuditEvents.AsNoTracking().OrderByDescending(row => row.Id).FirstAsync();
    Check(
        excluded.AfterJson!.Contains("Other fields changed") && !excluded.AfterJson.Contains("confidential"),
        "Excluded-field edits record the fact of change without copying sensitive content"
    );
    account.Name = "After";
    await context.SaveChangesAsync();
    var changed = await context.AuditEvents.AsNoTracking().OrderByDescending(row => row.Id).FirstAsync();
    Check(
        changed.BeforeJson!.Contains("Before") && changed.AfterJson!.Contains("After"),
        "Updates persist genuine previous and new values"
    );
    var count = await context.AuditEvents.CountAsync();
    await context.SaveChangesAsync();
    Check(await context.AuditEvents.CountAsync() == count, "No-op saves do not create audit noise");
    await using (var transaction = await context.Database.BeginTransactionAsync())
    {
        account.Name = "Rolled back";
        await context.SaveChangesAsync();
        await transaction.RollbackAsync();
    }

    context.ChangeTracker.Clear();
    Check(
        await context.AuditEvents.CountAsync() == count && (await context.Accounts.AsNoTracking().SingleAsync()).Name == "After",
        "Rolled-back changes and their audit entries both disappear"
    );
    await using (var one = new AccountingDbContext(auditOptions))
    await using (var two = new AccountingDbContext(auditOptions))
    {
        var a = await one.Accounts.SingleAsync();
        var b = await two.Accounts.SingleAsync();
        a.Name = "Concurrent winner";
        await one.SaveChangesAsync();
        var events = await context.AuditEvents.CountAsync();
        b.Name = "Stale update";
        try
        {
            await two.SaveChangesAsync();
            throw new Exception("Expected stale-update rejection");
        }
        catch (DbUpdateConcurrencyException)
        {
        }

        Check(
            await context.AuditEvents.CountAsync() == events,
            "Failed concurrency checks do not produce successful-change logs"
        );
    }

    var doomed = await context.Accounts.SingleAsync();
    context.Accounts.Remove(doomed);
    await context.SaveChangesAsync();
    var deleted = await context.AuditEvents.AsNoTracking().OrderByDescending(row => row.Id).FirstAsync();
    Check(
        deleted.Action == "Deleted" && deleted.AfterJson == null && deleted.BeforeJson!.Contains("Concurrent winner"),
        "Deletion retains the prior financial snapshot"
    );
    var activityFixtures = new[]
    {
        new AuditEvent
        {
            EntityId = "activity-retry", EntityType = "PaymentBankingLink", Action = "Updated",
            Actor = "System / background process", ActorKind = "System", Service = "BankingService.API",
            AtUtc = DateTimeOffset.UtcNow,
            BeforeJson = "{\"Amount\":85}",
            AfterJson = "{\"Amount\":85,\"Other fields changed (values excluded)\":\"NextAttemptAtUtc, UpdatedAtUtc\"}"
        },
        new AuditEvent
        {
            EntityId = "activity-money", EntityType = "PaymentBankingLink", Action = "Updated",
            ActorKind = "System", Service = "BankingService.API", AtUtc = DateTimeOffset.UtcNow,
            BeforeJson = "{\"Amount\":85}", AfterJson = "{\"Amount\":999}"
        },
        new AuditEvent
        {
            EntityId = "activity-excluded", EntityType = "PaymentBankingLink", Action = "Updated",
            ActorKind = "System", Service = "BankingService.API", AtUtc = DateTimeOffset.UtcNow,
            BeforeJson = "{\"Amount\":85}",
            AfterJson = "{\"Amount\":85,\"Other fields changed (values excluded)\":\"PaymentReference\"}"
        },
        new AuditEvent
        {
            EntityId = "activity-created", EntityType = "SalesInvoicePayment", Action = "Created",
            ActorKind = "System", Service = "SalesInvoiceService.API", AtUtc = DateTimeOffset.UtcNow,
            AfterJson = "{\"Amount\":85}"
        }
    };
    context.AuditEvents.AddRange(new AuditEvent
    {
        EntityId = "activity-reordered", EntityType = "PaymentBankingLink", Action = "Updated",
        ActorKind = "System", Service = "BankingService.API", AtUtc = DateTimeOffset.UtcNow,
        BeforeJson = "{\"Amount\":85,\"Currency\":\"USD\"}",
        AfterJson = "{\"Currency\":\"USD\",\"Amount\":85,\"Other fields changed (values excluded)\":\"UpdatedAtUtc\"}"
    }, new AuditEvent
    {
        EntityId = "activity-invalid", EntityType = "PaymentBankingLink", Action = "Updated",
        ActorKind = "System", Service = "BankingService.API", AtUtc = DateTimeOffset.UtcNow,
        BeforeJson = "invalid JSON", AfterJson = null
    }, new AuditEvent
    {
        EntityId = "activity-case", EntityType = "PaymentBankingLink", Action = "Updated",
        ActorKind = "System", Service = "BankingService.API", AtUtc = DateTimeOffset.UtcNow,
        BeforeJson = "{\"Currency\":\"usd\"}", AfterJson = "{\"Currency\":\"USD\"}"
    });
    context.AuditEvents.AddRange(activityFixtures);
    await context.SaveChangesAsync();
    await FinanceAuditBranchChecks();

    async Task FinanceAuditBranchChecks()
    {
        var writer = typeof(AccountingDbContext).Assembly.GetType("SparkFinance.Auditing.FinanceAudit")!
            .GetMethod("WriteAsync")!;
        await (Task)writer.Invoke(null, new object?[]
        {
            context, "Account", "branch-test", "Updated", null,
            new Dictionary<string, object?> { ["CompanyId"] = 2 }, CancellationToken.None
        })!;
        var mapped = await context.AuditEvents.AsNoTracking().OrderByDescending(row => row.Id).FirstAsync();
        Check(mapped.Branch == "Turbhe", "New accounting events resolve a uniquely mapped Turbhe company");
        await context.Database.ExecuteSqlRawAsync("INSERT INTO sales.BranchMasters VALUES ('Other branch',2)");
        await (Task)writer.Invoke(null, new object?[]
        {
            context, "Account", "ambiguous-test", "Updated", null,
            new Dictionary<string, object?> { ["CompanyId"] = 2 }, CancellationToken.None
        })!;
        var ambiguous = await context.AuditEvents.AsNoTracking().OrderByDescending(row => row.Id).FirstAsync();
        Check(ambiguous.Branch == null, "Shared company mappings do not guess a branch");
    }
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(typeof(AccountingDbContext).Assembly.Location)!
    };
    start.ArgumentList.Add(typeof(AccountingDbContext).Assembly.Location);
    start.ArgumentList.Add("--urls");
    start.ArgumentList.Add("http://127.0.0.1:5403");
    start.Environment["ConnectionStrings__DefaultConnection"] = connection;
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    server = new Process
    {
        StartInfo = start
    };
    server.OutputDataReceived += (
        _,
        e
    ) =>
    {
        if (e.Data != null)
            lock (output)
                output.AppendLine(e.Data);
    };
    server.ErrorDataReceived += (
        _,
        e
    ) =>
    {
        if (e.Data != null)
            lock (output)
                output.AppendLine(e.Data);
    };
    server.Start();
    server.BeginOutputReadLine();
    server.BeginErrorReadLine();
    using var client = new HttpClient
    {
        BaseAddress = new Uri("http://127.0.0.1:5403"),
        Timeout = TimeSpan.FromSeconds(15)
    };
    for (var attempt = 0;; attempt++)
    {
        try
        {
            using var ping = await client.GetAsync("/api/audit-history");
            if (ping.StatusCode == HttpStatusCode.Forbidden)
                break;
        }
        catch (HttpRequestException)
        {
        }

        if (attempt >= 60 || server.HasExited)
            throw new Exception("Test server did not start: " + output);
        await Task.Delay(200);
    }

    using (var denied = await client.GetAsync("/api/audit-history"))
        Check(denied.StatusCode == HttpStatusCode.Forbidden, "Audit API rejects missing administrator access");
    client.DefaultRequestHeaders.Add("X-Audit-Role", "Staff");
    using (var denied = await client.GetAsync("/api/audit-history"))
        Check(denied.StatusCode == HttpStatusCode.Forbidden, "Audit API rejects staff role");
    client.DefaultRequestHeaders.Remove("X-Audit-Role");
    client.DefaultRequestHeaders.Add("X-Audit-Role", "Admin");
    var page = JsonNode.Parse(await client.GetStringAsync("/api/audit-history?pageSize=1"))!;
    Check(
        page["data"]!.AsArray().Count == 1 && page["total"]!.GetValue<int>() > 1,
        "Audit events paginate on the server"
    );
    var filtered = JsonNode.Parse(await client.GetStringAsync("/api/audit-history?entityType=SalesInvoice&action=Snapshot&search=INV-BASELINE&branch=Office"))!;
    Check(
        filtered["total"]!.GetValue<int>() == 1,
        "Record, action, branch and text filters combine correctly"
    );
    var technical = JsonNode.Parse(await client.GetStringAsync("/api/audit-history?activity=Technical"))!;
    Check(technical["total"]!.GetValue<int>() == 2 &&
        technical["data"]!.AsArray().Any(row => row!["entityId"]!.GetValue<string>() == "activity-retry") &&
        technical["data"]!.AsArray().Any(row => row!["entityId"]!.GetValue<string>() == "activity-reordered"),
        "Technical view only contains timestamp-only background synchronization updates");
    var business = JsonNode.Parse(await client.GetStringAsync("/api/audit-history?activity=Business&pageSize=100"))!;
    var businessIds = business["data"]!.AsArray().Select(row => row!["entityId"]!.GetValue<string>()).ToArray();
    Check(businessIds.Contains("activity-money") && businessIds.Contains("activity-excluded") &&
        businessIds.Contains("activity-created") && !businessIds.Contains("activity-retry"),
        "Business view retains amounts, unknown excluded changes and created payments");
    Check(!business["data"]!.AsArray().Any(row => row!["action"]!.GetValue<string>() == "Snapshot"),
        "Baseline snapshots stay separate from business changes");
    Check(businessIds.Contains("activity-invalid") && businessIds.Contains("activity-case"),
        "Invalid snapshots load safely and case-sensitive financial changes remain visible");
    var technicalCsv = await client.GetStringAsync("/api/audit-history/export?activity=Technical");
    Check(technicalCsv.Contains("activity-retry") && !technicalCsv.Contains("activity-money"),
        "Export uses the same activity classification as the list");
    using (var invalidActivity = await client.GetAsync("/api/audit-history?activity=Unknown"))
        Check(invalidActivity.StatusCode == HttpStatusCode.BadRequest, "Unknown activity filters are rejected");
    using (var invalid = await client.GetAsync("/api/audit-history?from=2026-12-01T00:00:00Z&before=2026-11-01T00:00:00Z"))
        Check(invalid.StatusCode == HttpStatusCode.BadRequest, "Invalid date ranges are rejected");
    var lookups = JsonNode.Parse(await client.GetStringAsync("/api/audit-history/lookups"))!;
    Check(
        lookups["actions"]!.AsArray().Any(row => row!.GetValue<string>() == "Snapshot"),
        "Filter options come from stored audit data"
    );
    Check(lookups["branches"]!.AsArray().Any(row => row!.GetValue<string>() == "Turbhe") &&
        lookups["branches"]!.AsArray().Any(row => row!.GetValue<string>() == "Other branch"),
        "Branch filters include saved branches even without matching events");
    var csv = await client.GetStringAsync("/api/audit-history/export?entityType=Account");
    Check(csv.Contains("Before") && csv.Contains("Concurrent winner"), "CSV includes before/after snapshots");
    client.DefaultRequestHeaders.Add("X-Claim-Operator", "Local Reviewer");
    using (var response = await client.PostAsJsonAsync(
        "/api/accounts",
        new { companyId, code = "TEST-HTTP", name = "Local audit test", type = "Expense", normalBalance = "Debit", isPostingAccount = true, isActive = true }
    ))
        Check(response.IsSuccessStatusCode, "Existing account creation API still works with audit recording");
    var local = await context.AuditEvents.AsNoTracking().OrderByDescending(row => row.Id).FirstAsync();
    Check(
        local.Actor == "Local Reviewer" && local.ActorKind == "Local operator (unverified)",
        "Local identities are labelled unverified rather than authenticated"
    );
    Console.WriteLine($"All {checks} audit integration checks passed.");
}
catch
{
    Console.Error.WriteLine(output.ToString());
    throw;
}
finally
{
    if (server is { HasExited: false })
    {
        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
    }

    server?.Dispose();
    if (!database.StartsWith("OrbitAuditTests_", StringComparison.Ordinal) || database.Length != 48)
        throw new Exception("Unexpected test database name.");
    if (created)
        await expenses.Database.EnsureDeletedAsync();
}
