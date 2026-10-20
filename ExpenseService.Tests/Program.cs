using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ExpenseService.API.Data;
using Microsoft.EntityFrameworkCore;

// Run against a new LocalDB database. Never write test rows to SparkFinance.
var database = "OrbitExpenseTests_" + Guid.NewGuid().ToString("N");
var testServer = Environment.GetEnvironmentVariable("EXPENSE_TEST_SERVER") ?? "(localdb)\\MSSQLLocalDB";
var connection = "Server=" + testServer + ";Database=" + database + ";Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlServer(connection,
    sql => sql.MigrationsHistoryTable("__ExpenseMigrationsHistory", "expenses")).Options;
await using var context = new ExpenseDbContext(options);
Process? server = null;
var output = new StringBuilder();
var count = 0;
var created = false;

try
{
    await context.Database.MigrateAsync();
    created = true;
    var dll = typeof(ExpenseDbContext).Assembly.Location;
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(dll)!
    };
    start.ArgumentList.Add(dll);
    start.ArgumentList.Add("--urls");
    start.ArgumentList.Add("http://127.0.0.1:5398");
    start.Environment["ConnectionStrings__DefaultConnection"] = connection;
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
    server = new Process { StartInfo = start };
    server.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
    server.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
    server.Start();
    server.BeginOutputReadLine();
    server.BeginErrorReadLine();
    using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5398/api/expenses"), Timeout = TimeSpan.FromSeconds(15) };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            using var ping = await client.GetAsync("/api/expenses/companies");
            if (ping.IsSuccessStatusCode) break;
        }
        catch (HttpRequestException) { }
        if (attempt == 60 || server.HasExited) throw new Exception("Test server did not start. " + output);
        await Task.Delay(250);
    }

    async Task<JsonNode> Send(HttpMethod method, string path, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, "/api/expenses" + path);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expected) throw new Exception(method + " " + path + ": expected " + expected + ", got " + response.StatusCode + ": " + text);
        return JsonNode.Parse(text)!;
    }
    void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        count++;
    }
    JsonObject Expense(int companyId, int? accountId, string currency = "INR", decimal amount = 2300, decimal rate = 23, string status = "Draft") => new()
    {
        ["requestId"] = Guid.NewGuid().ToString(), ["companyId"] = companyId, ["bankAccountId"] = accountId,
        ["date"] = "2026-10-20", ["supplier"] = "Test Supplier", ["category"] = "Office",
        ["amount"] = amount, ["currency"] = currency, ["rate"] = rate, ["rateDate"] = "2026-10-20",
        ["status"] = status, ["notes"] = "Test only", ["reference"] = "Test"
    };

    var india = await Send(HttpMethod.Post, "/companies", new { name = "India", country = "IN" }, HttpStatusCode.Created);
    var dubai = await Send(HttpMethod.Post, "/companies", new { name = "Dubai", country = "AE" }, HttpStatusCode.Created);
    int indiaId = india["id"]!.GetValue<int>(), dubaiId = dubai["id"]!.GetValue<int>();
    Check(india["currency"]!.GetValue<string>() == "INR" && dubai["currency"]!.GetValue<string>() == "AED", "Company base currencies");
    var indiaBank = await Send(HttpMethod.Post, "/bank-accounts", new { companyId = indiaId, name = "HDFC", currency = "INR", lastFour = "1234" }, HttpStatusCode.Created);
    var indiaBank2 = await Send(HttpMethod.Post, "/bank-accounts", new { companyId = indiaId, name = "ICICI", currency = "INR" }, HttpStatusCode.Created);
    var dubaiBank = await Send(HttpMethod.Post, "/bank-accounts", new { companyId = dubaiId, name = "Emirates NBD", currency = "AED" }, HttpStatusCode.Created);
    await Send(HttpMethod.Post, "/bank-accounts", new { companyId = dubaiId, name = "ADCB", currency = "AED" }, HttpStatusCode.Created);
    int bankId = indiaBank["id"]!.GetValue<int>(), dubaiBankId = dubaiBank["id"]!.GetValue<int>();
    var scoped = await Send(HttpMethod.Get, "/bank-accounts?companyId=" + indiaId);
    Check(scoped.AsArray().Count == 2 && scoped.AsArray().All(row => row!["companyId"]!.GetValue<int>() == indiaId), "Two accounts per company, filtered by company");

    var body = Expense(indiaId, bankId);
    var draft = await Send(HttpMethod.Post, "", body, HttpStatusCode.Created);
    var id = draft["id"]!.GetValue<int>();
    var aed = await Send(HttpMethod.Get, "/" + id + "?displayCurrency=AED");
    Check(aed["displayAmount"]!.GetValue<decimal>() == 100m && aed["amount"]!.GetValue<decimal>() == 2300m && aed["currency"]!.GetValue<string>() == "INR", "Indian expense displays AED and preserves original INR");
    var inr = await Send(HttpMethod.Get, "/" + id + "?displayCurrency=INR");
    Check(inr["displayAmount"]!.GetValue<decimal>() == 2300m, "Switching back to INR preserves value");
    var retry = await Send(HttpMethod.Post, "", body, HttpStatusCode.Created);
    Check(retry["id"]!.GetValue<int>() == id, "Retrying a create does not duplicate expense");
    var reused = body.DeepClone().AsObject();
    reused["amount"] = 1000;
    await Send(HttpMethod.Post, "", reused, HttpStatusCode.BadRequest);
    Check(true, "Reused request ID cannot hide another expense");

    await Send(HttpMethod.Post, "", Expense(indiaId, dubaiBankId), HttpStatusCode.BadRequest);
    await Send(HttpMethod.Post, "", Expense(indiaId, bankId, "AED"), HttpStatusCode.BadRequest);
    await Send(HttpMethod.Post, "", Expense(indiaId, null, status: "Recorded"), HttpStatusCode.BadRequest);
    Check(true, "Cross-company account, wrong account currency and unpaid recorded expense rejected");
    await Send(HttpMethod.Post, "", Expense(indiaId, bankId, amount: 0), HttpStatusCode.BadRequest);
    await Send(HttpMethod.Post, "", Expense(indiaId, bankId, rate: 0), HttpStatusCode.BadRequest);
    await Send(HttpMethod.Post, "", Expense(indiaId, bankId, amount: 1.001m), HttpStatusCode.BadRequest);
    await Send(HttpMethod.Post, "", Expense(indiaId, bankId, rate: 23.1234567m), HttpStatusCode.BadRequest);
    await Send(HttpMethod.Get, "?displayCurrency=USD", expected: HttpStatusCode.BadRequest);
    await Send(HttpMethod.Get, "?page=0", expected: HttpStatusCode.BadRequest);
    await Send(HttpMethod.Get, "?from=2026-10-21&to=2026-10-20", expected: HttpStatusCode.BadRequest);
    Check(true, "Invalid amounts, rates, currencies, pagination and dates rejected");

    var stale = draft["rowVersion"]!.GetValue<string>();
    body["rowVersion"] = stale;
    body["notes"] = "Updated draft";
    var updated = await Send(HttpMethod.Put, "/" + id, body);
    await Send(HttpMethod.Put, "/" + id, body, HttpStatusCode.Conflict);
    Check(updated["notes"]!.GetValue<string>() == "Updated draft", "Draft editing and stale-version rejection");
    var move = body.DeepClone().AsObject();
    move["rowVersion"] = updated["rowVersion"]!.GetValue<string>();
    move["companyId"] = dubaiId;
    move["bankAccountId"] = null;
    await Send(HttpMethod.Put, "/" + id, move, HttpStatusCode.BadRequest);
    Check(true, "Draft cannot be moved between companies");

    async Task<HttpResponseMessage> Upload(byte[] bytes, string name, string version)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        form.Add(new StringContent(version), "rowVersion");
        return await client.PostAsync("/api/expenses/" + id + "/receipt", form);
    }
    using (var invalidReceipt = await Upload(Encoding.UTF8.GetBytes("not a PDF"), "fake.pdf", updated["rowVersion"]!.GetValue<string>()))
    {
        Check(invalidReceipt.StatusCode == HttpStatusCode.BadRequest, "Receipt content validated rather than trusting extension");
    }
    var pdf = Encoding.UTF8.GetBytes("%PDF-1.4\nTest receipt\n%%EOF");
    using (var upload = await Upload(pdf, "receipt.pdf", updated["rowVersion"]!.GetValue<string>()))
    {
        var text = await upload.Content.ReadAsStringAsync();
        Check(upload.IsSuccessStatusCode, "Draft receipt upload: " + (upload.IsSuccessStatusCode ? "saved" : text));
        updated = JsonNode.Parse(text)!;
    }
    using (var receipt = await client.GetAsync("/api/expenses/" + id + "/receipt"))
    {
        Check(receipt.IsSuccessStatusCode && (await receipt.Content.ReadAsByteArrayAsync()).SequenceEqual(pdf), "Receipt downloads original bytes");
    }
    using (var staleReceipt = await Upload(pdf, "receipt.pdf", stale))
    {
        Check(staleReceipt.StatusCode == HttpStatusCode.Conflict, "Stale receipt upload rejected");
    }
    body["rowVersion"] = updated["rowVersion"]!.GetValue<string>();
    body["status"] = "Recorded";
    var recorded = await Send(HttpMethod.Put, "/" + id, body);
    body["rowVersion"] = recorded["rowVersion"]!.GetValue<string>();
    await Send(HttpMethod.Put, "/" + id, body, HttpStatusCode.Conflict);
    using (var lockedReceipt = await Upload(pdf, "receipt.pdf", body["rowVersion"]!.GetValue<string>()))
    {
        Check(recorded["status"]!.GetValue<string>() == "Recorded" && lockedReceipt.StatusCode == HttpStatusCode.Conflict, "Recorded expense and receipt are locked");
    }

    await Send(HttpMethod.Post, "", Expense(dubaiId, dubaiBankId, "AED", 100, 24, "Recorded"), HttpStatusCode.Created);
    await Send(HttpMethod.Post, "", Expense(indiaId, bankId, amount: 2400, rate: 24, status: "Recorded"), HttpStatusCode.Created);
    var allAed = await Send(HttpMethod.Get, "?displayCurrency=AED&pageSize=1");
    var allInr = await Send(HttpMethod.Get, "?displayCurrency=INR");
    var onlyIndia = await Send(HttpMethod.Get, "?displayCurrency=AED&companyId=" + indiaId);
    Check(allAed["recorded"]!.GetValue<decimal>() == 300m && allAed["data"]!.AsArray().Count == 1 &&
        allInr["recorded"]!.GetValue<decimal>() == 7100m && onlyIndia["recorded"]!.GetValue<decimal>() == 200m,
        "Totals use each saved rate across every page and honor company filter");
    var dubaiInr = await Send(HttpMethod.Get, "?companyId=" + dubaiId + "&displayCurrency=INR");
    Check(dubaiInr["data"]![0]!["displayAmount"]!.GetValue<decimal>() == 2400m, "Dubai AED expense converts to INR using its own rate");

    await Send(HttpMethod.Post, "/" + id + "/void", new { rowVersion = stale, reason = "Duplicate" }, HttpStatusCode.Conflict);
    await Send(HttpMethod.Post, "/" + id + "/void", new { rowVersion = recorded["rowVersion"]!.GetValue<string>(), reason = " " }, HttpStatusCode.BadRequest);
    await Send(HttpMethod.Post, "/" + id + "/void", new { rowVersion = recorded["rowVersion"]!.GetValue<string>(), reason = "Duplicate" });
    var afterVoid = await Send(HttpMethod.Get, "?displayCurrency=AED");
    var voided = await Send(HttpMethod.Get, "/" + id);
    Check(afterVoid["recorded"]!.GetValue<decimal>() == 200m && afterVoid["voided"]!.GetValue<decimal>() == 100m &&
        voided["voidReason"]!.GetValue<string>() == "Duplicate" && voided["amount"]!.GetValue<decimal>() == 2300m,
        "Voiding keeps original values and reason, and excludes expense from recorded total");

    var constraints = await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.foreign_keys WHERE name LIKE 'FK_Expenses_BankAccounts%'").SingleAsync();
    Check(constraints == 1, "Database enforces company/account ownership foreign key");
    await Send(HttpMethod.Post, "/companies", new { name = "India", country = "IN" }, HttpStatusCode.Conflict);
    await Send(HttpMethod.Post, "/bank-accounts", new { companyId = indiaId, name = "HDFC", currency = "INR" }, HttpStatusCode.Conflict);
    Check(true, "Duplicate company/account names produce clear conflict responses");
    var uiTest = Environment.GetEnvironmentVariable("EXPENSE_UI_TEST");
    if (!string.IsNullOrWhiteSpace(uiTest))
    {
        var uiStart = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        uiStart.ArgumentList.Add(uiTest);
        using var ui = Process.Start(uiStart)!;
        await ui.WaitForExitAsync();
        if (ui.ExitCode != 0) throw new Exception("Browser tests failed.");
    }
    Console.WriteLine("All " + count + " integration checks passed.");
}
finally
{
    if (server is { HasExited: false })
    {
        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
    }
    server?.Dispose();
    // Only this uniquely named database, created above, may be removed.
    if (!database.StartsWith("OrbitExpenseTests_", StringComparison.Ordinal) || database.Length != 50)
        throw new InvalidOperationException("Unexpected test database name.");
    if (created) await context.Database.EnsureDeletedAsync();
}
