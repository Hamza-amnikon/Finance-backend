using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

// Isolated test database only; never seed or edit SparkFinance.
var database = "FinanceTaxTests_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"(localdb)\MSSQLLocalDB",
    InitialCatalog = database,
    IntegratedSecurity = true,
    TrustServerCertificate = true
};
var masterConnection = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
Process? server = null;
var output = new StringBuilder();
var created = false;
var checks = 0;
void Check(bool value, string description)
{
    if (!value)
    {
        throw new Exception("FAIL: " + description);
    }

    Console.WriteLine("PASS: " + description);
    checks++;
}
async Task Sql(SqlConnection sql, string text)
{
    await using var command = sql.CreateCommand();
    command.CommandText = text;
    await command.ExecuteNonQueryAsync();
}
try
{
    await using var master = new SqlConnection(masterConnection.ConnectionString);
    await master.OpenAsync();
    await Sql(master, $"CREATE DATABASE [{database}]");
    created = true;
    await using (var sql = new SqlConnection(connection.ConnectionString))
    {
        await sql.OpenAsync();
        await Sql(sql, "CREATE SCHEMA expenses");
        await Sql(sql, "CREATE TABLE expenses.Companies (Id int PRIMARY KEY,Name nvarchar(160),Country nvarchar(2),Currency nvarchar(3)); INSERT INTO expenses.Companies VALUES(1,'Dubai test','AE','AED'),(2,'Turbhe test','IN','INR');");
        var setup = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "AddTaxSettings.sql"));
        await Sql(sql, setup);
        await Sql(sql, setup);
    }
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = AppContext.BaseDirectory
    };
    start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AccountingService.API.dll"));
    start.ArgumentList.Add("--urls");
    start.ArgumentList.Add("http://127.0.0.1:5419");
    start.Environment["ConnectionStrings__DefaultConnection"] = connection.ConnectionString;
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    server = new Process { StartInfo = start };
    server.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (output) output.AppendLine(e.Data); } };
    server.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (output) output.AppendLine(e.Data); } };
    server.Start();
    server.BeginOutputReadLine();
    server.BeginErrorReadLine();
    using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5419"), Timeout = TimeSpan.FromSeconds(15) };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            using var ping = await client.GetAsync("/api/tax-settings/lookups");
            if (ping.IsSuccessStatusCode)
            {
                break;
            }
        }
        catch (HttpRequestException) { }
        if (attempt > 50 || server.HasExited)
        {
            throw new Exception("Test API failed to start.");
        }

        await Task.Delay(200);
    }
    var setupData = JsonNode.Parse(await client.GetStringAsync("/api/tax-settings/lookups"))!;
    var types = setupData["types"]!.AsArray();
    Check(types.Count == 3, "Setup can be rerun without duplicate tax types");
    var vat = types.Single(row => row!["countryCode"]!.GetValue<string>() == "AE")!;
    var local = types.Single(row => row!["code"]!.GetValue<string>() == "GST_LOCAL")!;
    Check(local["components"]!.AsArray().Count == 2 && vat["components"]!.AsArray().Count == 1,
        "Country-specific component fields come from the database");
    JsonObject Draft(int companyId, JsonNode type, string code)
    {
        return new JsonObject
        {
            ["companyId"] = companyId,
            ["taxTypeId"] = type["id"]!.GetValue<int>(),
            ["code"] = code,
            ["name"] = "Illustrative test configuration",
            ["effectiveFrom"] = "2026-11-01",
            ["effectiveTo"] = null,
            ["isActive"] = true,
            ["components"] = new JsonArray(type["components"]!.AsArray().Select(name => (JsonNode)new JsonObject
            {
                ["name"] = name!.GetValue<string>(),
                ["rate"] = 1.25m
            }).ToArray())
        };
    }
    using (var bad = await client.PostAsJsonAsync("/api/tax-settings", Draft(2, vat, "CROSS")))
        Check(bad.StatusCode == HttpStatusCode.BadRequest, "Turbhe cannot save UAE tax types");
    using (var bad = await client.PostAsJsonAsync("/api/tax-settings", Draft(1, local, "CROSS")))
        Check(bad.StatusCode == HttpStatusCode.BadRequest, "Dubai cannot save India tax types");
    var draft = Draft(1, vat, "TEST");
    using var savedResponse = await client.PostAsJsonAsync("/api/tax-settings", draft);
    Check(savedResponse.IsSuccessStatusCode, "Dubai tax configuration saves");
    var saved = JsonNode.Parse(await savedResponse.Content.ReadAsStringAsync())!;
    var id = saved["id"]!.GetValue<int>();
    Check(saved["rate"]!.GetValue<decimal>() == 1.25m, "Total is derived from entered components");
    var indiaDraft = Draft(2, local, "TEST");
    using (var india = await client.PostAsJsonAsync("/api/tax-settings", indiaDraft))
        Check(india.IsSuccessStatusCode, "Turbhe keeps a separate rate using the same tax code");
    using (var duplicate = await client.PostAsJsonAsync("/api/tax-settings", draft))
        Check(duplicate.StatusCode == HttpStatusCode.BadRequest, "Overlapping versions of a company tax code are rejected");
    var reversed = Draft(1, vat, "DATES");
    reversed["effectiveTo"] = "2026-10-31";
    using (var bad = await client.PostAsJsonAsync("/api/tax-settings", reversed))
        Check(bad.StatusCode == HttpStatusCode.BadRequest, "Reversed effective dates are rejected");
    var invalidRate = Draft(1, vat, "RATE");
    invalidRate["components"]![0]!["rate"] = -1;
    using (var bad = await client.PostAsJsonAsync("/api/tax-settings", invalidRate))
        Check(bad.StatusCode == HttpStatusCode.BadRequest, "Negative rates are rejected");
    var incomplete = Draft(2, local, "COMPONENTS");
    incomplete["components"]!.AsArray().RemoveAt(1);
    using (var bad = await client.PostAsJsonAsync("/api/tax-settings", incomplete))
        Check(bad.StatusCode == HttpStatusCode.BadRequest, "Missing configured components are rejected");
    var edited = (JsonObject)draft.DeepClone();
    edited["rowVersion"] = saved["rowVersion"]!.GetValue<string>();
    edited["isActive"] = false;
    using (var changed = await client.PutAsJsonAsync($"/api/tax-settings/{id}", edited))
        Check(changed.IsSuccessStatusCode, "A saved rate can be deactivated");
    var missingVersion = (JsonObject)edited.DeepClone();
    missingVersion["rowVersion"] = null;
    using (var badVersion = await client.PutAsJsonAsync($"/api/tax-settings/{id}", missingVersion))
        Check(badVersion.StatusCode == HttpStatusCode.BadRequest, "Missing edit versions are rejected cleanly");
    using (var stale = await client.PutAsJsonAsync($"/api/tax-settings/{id}", edited))
        Check(stale.StatusCode == HttpStatusCode.Conflict, "Stale edits are rejected by row version");
    var list = JsonNode.Parse(await client.GetStringAsync("/api/tax-settings?companyId=1"))!.AsArray();
    Check(list.Count == 1 && !list[0]!["isActive"]!.GetValue<bool>(), "Reload preserves the saved status and company scope");
    Console.WriteLine($"All {checks} tax-setting integration checks passed.");
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
        server.Kill(true);
        await server.WaitForExitAsync();
    }
    server?.Dispose();
    if (created && database.StartsWith("FinanceTaxTests_") && database.Length == 48)
    {
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(masterConnection.ConnectionString);
        await master.OpenAsync();
        await Sql(master, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
    }
}
