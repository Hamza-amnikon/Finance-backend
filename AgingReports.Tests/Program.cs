using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AccountingService.API.Data;
using Microsoft.Data.SqlClient;

// Only this uniquely named test database is created, populated and removed.
var database = "OrbitAgingTests_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
await using var master = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
await master.OpenAsync();
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
    {
        throw new Exception("FAIL: " + name);
    }

    Console.WriteLine("PASS: " + name);
    checks++;
}

try
{
    await new SqlCommand($"CREATE DATABASE [{database}]", master).ExecuteNonQueryAsync();
    created = true;
    await using (var sql = new SqlConnection(connection))
    {
        await sql.OpenAsync();
        foreach (var schema in new[]
        {
            "sales",
            "purchases"
        }

        )
        {
            await new SqlCommand($"CREATE SCHEMA [{schema}]", sql).ExecuteNonQueryAsync();
        }

        await new SqlCommand(
                    """
            CREATE TABLE sales.SalesInvoices (Id int, InvoiceNumber nvarchar(50), ClientId uniqueidentifier, ClientName nvarchar(200), Branch nvarchar(100), Currency nvarchar(3), InvoiceDate date, DueDate date, GrandTotal decimal(18,2), Status nvarchar(20));
            CREATE TABLE sales.SalesInvoicePayments (SalesInvoiceId int, Amount decimal(18,2), PaymentDate date, Status nvarchar(20) NOT NULL DEFAULT 'Recorded');
            CREATE TABLE sales.SalesCreditNotes (SalesInvoiceId int, GrandTotal decimal(18,2), CreditNoteDate date, Status nvarchar(20));
            CREATE TABLE sales.CustomerCreditApplications (TargetInvoiceId int, Amount decimal(18,2), AppliedAtUtc datetime2);
            CREATE TABLE purchases.Bills (Id int, BillNumber nvarchar(50), VendorId uniqueidentifier, VendorName nvarchar(200), Branch nvarchar(100), Currency nvarchar(3), BillDate date, DueDate date, GrandTotal decimal(18,2), Status nvarchar(20));
            CREATE TABLE purchases.PaymentsMade (BillId int, Amount decimal(18,2), PaymentDate date, Status nvarchar(20));
            CREATE TABLE purchases.VendorCreditApplications (BillId int, Amount decimal(18,2), ApplicationDate date, Status nvarchar(20));
            DECLARE @party uniqueidentifier = NEWID();
            INSERT INTO sales.SalesInvoices VALUES
                (1,'INV-1',@party,'Customer','Branch A','INR','2025-10-01','2026-10-05',1000,'Issued'),
                (2,'INV-PAID',@party,'Customer','Branch A','INR','2026-10-01','2026-10-05',100,'Issued'),
                (3,'INV-DRAFT',@party,'Customer','Branch A','INR','2026-10-01','2026-10-05',100,'Draft'),
                (4,'INV-FUTURE',@party,'Customer','Branch A','INR','2026-11-01','2026-11-05',100,'Issued');
            INSERT INTO sales.SalesInvoicePayments (SalesInvoiceId,Amount,PaymentDate,Status) VALUES(1,999999,'2026-10-10','Voided');
            INSERT INTO sales.SalesInvoicePayments (SalesInvoiceId,Amount,PaymentDate) VALUES (1,100,'2026-10-10'),(1,200,'2026-11-01'),(2,100,'2026-10-01');
            INSERT INTO sales.SalesCreditNotes VALUES (1,50,'2026-10-15','Issued'),(1,500,'2026-10-15','Draft'),(1,30,'2026-11-02','Issued');
            INSERT INTO sales.CustomerCreditApplications VALUES (1,20,'2026-10-20'),(1,10,'2026-11-02');
            INSERT INTO purchases.Bills VALUES (1,'BILL-1',@party,'Vendor','Branch B','INR','2026-09-01',NULL,500,'Issued'),(2,'BILL-DRAFT',@party,'Vendor','Branch B','INR','2026-10-01',NULL,900,'Draft');
            INSERT INTO purchases.PaymentsMade VALUES (1,100,'2026-10-12','Recorded'),(1,50,'2026-11-02','Recorded'),(1,40,'2026-10-15','Voided');
            INSERT INTO purchases.VendorCreditApplications VALUES (1,50,'2026-10-20','Applied'),(1,25,'2026-11-02','Applied'),(1,20,'2026-10-20','Voided');
            """,
                    sql
                ).ExecuteNonQueryAsync();
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
    start.ArgumentList.Add("http://127.0.0.1:5401");
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
        BaseAddress = new Uri("http://127.0.0.1:5401"),
        Timeout = TimeSpan.FromSeconds(15)
    };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            using var ping = await client.GetAsync("/api/aging-reports");
            if (ping.StatusCode == HttpStatusCode.BadRequest)
            {
                break;
            }
        }
        catch (HttpRequestException)
        {
        }

        if (attempt >= 60 || server.HasExited)
        {
            throw new Exception("Test server did not start: " + output);
        }

        await Task.Delay(200);
    }

    async Task<JsonArray> Rows(string date)
    {
        using var response = await client.GetAsync("/api/aging-reports?asOf=" + date);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(text + output);
        }

        return JsonNode.Parse(text)!["rows"]!.AsArray();
    }

    var rows = await Rows("2026-10-31");
    Check(rows.Count == 2, "Only outstanding issued documents are included");
    var invoice = rows.Single(row => row!["kind"]!.GetValue<string>() == "Receivable")!;
    var bill = rows.Single(row => row!["kind"]!.GetValue<string>() == "Payable")!;
    Check(
        invoice["balance"]!.GetValue<decimal>() == 830,
        "Invoice payments, issued credits and applied customer credits reduce the balance"
    );
    Check(invoice["date"]!.GetValue<string>() == "2025-10-01", "Old outstanding invoices remain included");
    Check(
        bill["balance"]!.GetValue<decimal>() == 350 && bill["dueDate"] == null,
        "Bill payments and applied credits exclude future and voided settlements; missing due date is preserved"
    );
    Check(
        invoice["credits"]!.GetValue<decimal>() == 70 && bill["credits"]!.GetValue<decimal>() == 50,
        "Draft and future credits are excluded"
    );
    var earlier = await Rows("2026-10-01");
    Check(
        earlier.Single(row => row!["kind"]!.GetValue<string>() == "Receivable")!["balance"]!.GetValue<decimal>() == 1000,
        "Earlier cutoff ignores later settlements"
    );
    Check((await Rows("2000-01-01")).Count == 0, "Empty historical range returns no balances");
    using var invalid = await client.GetAsync("/api/aging-reports?asOf=9999-12-31");
    Check(invalid.StatusCode == HttpStatusCode.BadRequest, "Future cutoffs are rejected");
    Console.WriteLine($"All {checks} aging integration checks passed.");
}
finally
{
    if (server is { HasExited: false })
    {
        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
    }

    server?.Dispose();
    if (!database.StartsWith("OrbitAgingTests_", StringComparison.Ordinal) || database.Length != 48)
    {
        throw new Exception("Unexpected test database name.");
    }

    if (created)
    {
        await new SqlCommand(
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];",
                master
            ).ExecuteNonQueryAsync();
    }
}
