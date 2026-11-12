using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ExpenseService.API.Data;
using ExpenseService.API.Models;
using AccountingService.API.Data;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

// Full workflow tests run only in a new, uniquely named database; never in SparkFinance.
var database = "OrbitClaimTests_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var expenses = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__ExpenseMigrationsHistory", "expenses")).Options);
await using var accounting = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__AccountingMigrationsHistory", "accounting")).Options);
Process? server = null;
var output = new StringBuilder();
var created = false;
var checks = 0;
void Check(
    bool condition,
    string message
)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + message);
    }

    Console.WriteLine("PASS: " + message);
    checks++;
}

try
{
    await expenses.Database.MigrateAsync();
    created = true;
    await accounting.Database.MigrateAsync();
    expenses.Companies.Add(new Company { Name = "Claim Test Office", Country = "IN", Currency = "INR" });
    await expenses.SaveChangesAsync();
    var companyId = expenses.Companies.Single().Id;
    await expenses.Database.ExecuteSqlRawAsync("""
        CREATE SCHEMA sales;
        """);
    await expenses.Database.ExecuteSqlRawAsync("""
        CREATE TABLE sales.CurrencyMasters (CurrencyCode nvarchar(3) NOT NULL, CurrencyName nvarchar(100) NOT NULL, DecimalPlaces int NOT NULL, IsActive bit NOT NULL);
        INSERT INTO sales.CurrencyMasters VALUES ('INR','Indian Rupee',2,1);
        IF SCHEMA_ID(N'amnikon') IS NULL EXEC(N'CREATE SCHEMA amnikon'); CREATE TABLE amnikon.Branches (Name nvarchar(100), CompanyName nvarchar(200), IsActive bit, AccountingCompanyId int);
        """);
    await expenses.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO amnikon.Branches VALUES ('Claim Test Office','Claim Test Office',1,{companyId})");
    var expense = new Account
    {
        CompanyId = companyId,
        Code = "E1",
        Name = "Travel",
        Type = AccountType.Expense,
        NormalBalance = AccountNormalBalance.Debit
    };
    var payable = new Account
    {
        CompanyId = companyId,
        Code = "L1",
        Name = "Employee payable",
        Type = AccountType.Liability,
        NormalBalance = AccountNormalBalance.Credit
    };
    var cash = new Account
    {
        CompanyId = companyId,
        Code = "A1",
        Name = "Cash",
        Type = AccountType.Asset,
        NormalBalance = AccountNormalBalance.Debit
    };
    accounting.Accounts.AddRange(expense, payable, cash);
    await accounting.SaveChangesAsync();
    var dll = typeof(ExpenseDbContext).Assembly.Location;
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(dll)!
    };
    start.ArgumentList.Add(dll);
    start.ArgumentList.Add("--urls");
    start.ArgumentList.Add("http://127.0.0.1:5399");
    start.Environment["ConnectionStrings__DefaultConnection"] = connection;
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ExchangeRates__BaseUrl"] = "https://localhost:7441/api/exchange-rates/";
    start.Environment["ExchangeRates__ProviderName"] = "Test service (same-currency checks only)";
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
        {
            lock (output)
                output.AppendLine(e.Data);
        }
    };
    server.ErrorDataReceived += (
        _,
        e
    ) =>
    {
        if (e.Data != null)
        {
            lock (output)
                output.AppendLine(e.Data);
        }
    };
    server.Start();
    server.BeginOutputReadLine();
    server.BeginErrorReadLine();
    using var client = new HttpClient
    {
        BaseAddress = new Uri("http://127.0.0.1:5399"),
        Timeout = TimeSpan.FromSeconds(15)
    };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            using var ping = await client.GetAsync("/api/expense-claims");
            if (ping.StatusCode == HttpStatusCode.Unauthorized)
            {
                break;
            }
        }
        catch (HttpRequestException)
        {
        }

        if (attempt >= 60 || server.HasExited)
        {
            throw new Exception("Test server failed to start: " + output);
        }

        await Task.Delay(200);
    }

    async Task<JsonNode> Send(
        HttpMethod method,
        string path,
        object? body = null,
        string actor = "Owner",
        string role = "Admin",
        HttpStatusCode expected = HttpStatusCode.OK
    )
    {
        using var request = new HttpRequestMessage(method, "/api/expense-claims" + path);
        request.Headers.Add("X-Claim-Operator", actor);
        request.Headers.Add("X-Claim-Role", role);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expected)
        {
            throw new Exception($"{method} {path}: expected {expected}, got {response.StatusCode}: {text}\n{output}");
        }

        return JsonNode.Parse(text)!;
    }

    JsonObject Draft(string reference) => new()
    {
        ["requestId"] = Guid.NewGuid().ToString(),
        ["rowVersion"] = "",
        ["companyId"] = companyId,
        ["branch"] = "Claim Test Office",
        ["currency"] = "INR",
        ["date"] = "2026-10-20",
        ["reference"] = reference,
        ["employee"] = "Owner",
        ["category"] = "Travel",
        ["purpose"] = "Client meeting",
        ["notes"] = "",
        ["amount"] = 100
    };
    object Action(
        JsonNode row,
        string status,
        string reason = ""
    ) => new
    {
        status,
        reason,
        rowVersion = row["rowVersion"]!.GetValue<string>(),
        approvedDate = "2026-10-21",
        paidDate = "2026-10-22",
        paymentReference = "TEST-PAY",
        expenseAccountId = expense.Id,
        payableAccountId = payable.Id,
        paymentAccountId = cash.Id
    };
    var body = Draft("CLM-TEST-1");
    var row = await Send(HttpMethod.Post, "", body);
    var id = row["id"]!.GetValue<int>();
    var retry = await Send(HttpMethod.Post, "", body);
    Check(
        retry["id"]!.GetValue<int>() == id && await expenses.ExpenseClaims.CountAsync() == 1,
        "Create retry does not duplicate a claim"
    );
    var changedRetry = body.DeepClone().AsObject();
    changedRetry["amount"] = 200;
    await Send(HttpMethod.Post, "", changedRetry, expected: HttpStatusCode.Conflict);
    Check(true, "Reused request IDs reject changed claim details");
    Check(
        (await Send(HttpMethod.Get, "", actor: "Other", role: "Staff")).AsArray().Count == 0,
        "Staff cannot list another operator's claims"
    );
    await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Paid"), expected: HttpStatusCode.Conflict);
    Check(true, "Cannot pay a draft");
    var stale = row["rowVersion"]!.GetValue<string>();
    body["rowVersion"] = stale;
    body["amount"] = 125;
    row = await Send(HttpMethod.Put, $"/{id}", body);
    body["rowVersion"] = stale;
    await Send(HttpMethod.Put, $"/{id}", body, expected: HttpStatusCode.Conflict);
    Check(true, "Stale edits are rejected");
    using (var upload = new HttpRequestMessage(HttpMethod.Post, $"/api/expense-claims/{id}/receipts"))
    {
        upload.Headers.Add("X-Claim-Operator", "Owner");
        upload.Headers.Add("X-Claim-Role", "Admin");
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(row["rowVersion"]!.GetValue<string>()), "rowVersion");
        content.Add(new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nTest receipt")), "file", "receipt.pdf");
        upload.Content = content;
        using var response = await client.SendAsync(upload);
        Check(response.IsSuccessStatusCode, "Receipt upload accepts a PDF");
        row = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    var receiptId = row["receipts"]![0]!["id"]!.GetValue<int>();
    using (var get = new HttpRequestMessage(HttpMethod.Get, $"/api/expense-claims/{id}/receipts/{receiptId}"))
    {
        get.Headers.Add("X-Claim-Operator", "Owner");
        get.Headers.Add("X-Claim-Role", "Admin");
        using var response = await client.SendAsync(get);
        Check(
            response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "application/pdf",
            "Receipt downloads with original type"
        );
    }

    row = await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Submitted"));
    using (var upload = new HttpRequestMessage(HttpMethod.Post, $"/api/expense-claims/{id}/receipts"))
    {
        upload.Headers.Add("X-Claim-Operator", "Owner");
        upload.Headers.Add("X-Claim-Role", "Admin");
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(row["rowVersion"]!.GetValue<string>()), "rowVersion");
        content.Add(new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nTest receipt")), "file", "receipt.pdf");
        upload.Content = content;
        using var response = await client.SendAsync(upload);
        Check(response.StatusCode == HttpStatusCode.Conflict, "Submitted claim receipts are locked");
    }

    await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Approved"), expected: HttpStatusCode.Conflict);
    await Send(
        HttpMethod.Patch,
        $"/{id}/status",
        Action(row, "Approved"),
        actor: "Reviewer",
        role: "Staff",
        expected: HttpStatusCode.Conflict
    );
    Check(true, "Self-approval and staff approval are rejected");
    await Send(
        HttpMethod.Patch,
        $"/{id}/status",
        Action(row, "Rejected"),
        actor: "Reviewer",
        expected: HttpStatusCode.BadRequest
    );
    row = await Send(
        HttpMethod.Patch,
        $"/{id}/status",
        Action(row, "Rejected", "Receipt clarification"),
        actor: "Reviewer"
    );
    row = await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Draft"));
    row = await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Submitted"));
    row = await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Approved"), actor: "Reviewer");
    Check(
        row["status"]!.GetValue<string>() == "Approved" && row["approvedBy"]!.GetValue<string>() == "Reviewer",
        "Reject, return to draft, resubmit, and independent approval"
    );
    var approval = await accounting.JournalEntries.AsNoTracking().Include(x => x.Lines).SingleAsync();
    Check(
        approval.Status == JournalEntryStatus.Posted && approval.TotalDebit == 125 && approval.TotalCredit == 125 && approval.Lines.Count == 2,
        "Approval posts a balanced journal once"
    );
    body["rowVersion"] = row["rowVersion"]!.GetValue<string>();
    await Send(HttpMethod.Put, $"/{id}", body, expected: HttpStatusCode.Conflict);
    Check(true, "Approved claims are locked for editing");
    row = await Send(HttpMethod.Patch, $"/{id}/status", Action(row, "Paid"), actor: "Reviewer");
    var journals = await accounting.JournalEntries.AsNoTracking().Include(x => x.Lines).ToListAsync();
    Check(
        journals.Count == 2 && journals.Sum(x => x.Lines.Where(l => l.AccountId == payable.Id).Sum(l => l.DebitAmount - l.CreditAmount)) == 0,
        "Reimbursement settles employee payable without adding another expense"
    );
    await Send(
        HttpMethod.Patch,
        $"/{id}/status",
        Action(row, "Paid"),
        actor: "Reviewer",
        expected: HttpStatusCode.Conflict
    );
    Check(
        await accounting.JournalEntries.CountAsync() == 2,
        "Repeated payment cannot duplicate accounting entries"
    );
    var cancelled = await Send(HttpMethod.Post, "", Draft("CLM-CANCEL"));
    cancelled = await Send(HttpMethod.Patch, $"/{cancelled["id"]}/status", Action(cancelled, "Cancelled"));
    Check(cancelled["status"]!.GetValue<string>() == "Cancelled", "Draft cancellation preserved");
    Check(await expenses.ExpenseClaimHistory.CountAsync() >= 9, "Persistent workflow audit history");
    await expenses.Database.ExecuteSqlRawAsync("CREATE SCHEMA purchases;");
    await expenses.Database.ExecuteSqlRawAsync("""
        CREATE TABLE purchases.Bills (Id int, VendorId uniqueidentifier, VendorName nvarchar(200), BillNumber nvarchar(50), Branch nvarchar(100), Currency nvarchar(3), BillDate date, DueDate date, GrandTotal decimal(18,2), TaxTotal decimal(18,2), Status nvarchar(20));
        CREATE TABLE purchases.PaymentsMade (Id int, BillId int, PaymentNumber nvarchar(50), VendorId uniqueidentifier, VendorName nvarchar(200), Branch nvarchar(100), Currency nvarchar(3), PaymentDate date, Amount decimal(18,2), Status nvarchar(20));
        CREATE TABLE purchases.VendorCredits (Id int, VendorId uniqueidentifier, VendorName nvarchar(200), VendorCreditNumber nvarchar(50), Branch nvarchar(100), Currency nvarchar(3), CreditDate date, GrandTotal decimal(18,2), TaxTotal decimal(18,2), Status nvarchar(20));
        CREATE TABLE purchases.VendorCreditApplications (BillId int, Amount decimal(18,2), ApplicationDate date, Status nvarchar(20));
        """);
    var report = JsonNode.Parse(await client.GetStringAsync("/api/expense-reports?fromDate=2026-10-01&toDate=2026-10-31"))!;
    Check(
        report["rows"]!.AsArray().Count == 2 && report["rows"]!.AsArray().Single(row => row!["type"]!.GetValue<string>() == "Claim")!["net"]!.GetValue<decimal>() == 125,
        "Expense report includes approved claim cost and separate reimbursement"
    );
    var beforeApproval = JsonNode.Parse(await client.GetStringAsync("/api/expense-reports?fromDate=2026-10-01&toDate=2026-10-20"))!;
    Check(beforeApproval["rows"]!.AsArray().Count == 0, "Claim cost is excluded before its approval date");
    var beforePayment = JsonNode.Parse(await client.GetStringAsync("/api/expense-reports?fromDate=2026-10-01&toDate=2026-10-21"))!;
    Check(
        beforePayment["rows"]!.AsArray().Single()!["balance"]!.GetValue<decimal>() == 125,
        "Historical claim balance remains unpaid before reimbursement date"
    );
    Console.WriteLine($"All {checks} claim integration checks passed.");
}
finally
{
    if (server is { HasExited: false })
    {
        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
    }

    server?.Dispose();
    if (!database.StartsWith("OrbitClaimTests_", StringComparison.Ordinal) || database.Length != 48)
    {
        throw new Exception("Unexpected test database name.");
    }

    if (created)
    {
        await expenses.Database.EnsureDeletedAsync();
    }
}
