using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.Data.SqlClient;

// All fixtures belong to this disposable database, never SparkFinance.
var database = "OrbitReconTests_" + Guid.NewGuid().ToString("N");
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
    Check(
        ReconciliationRules.Equal(1m, 1.004m, 2) && !ReconciliationRules.Equal(1m, 1.005m, 2),
        "Comparison respects configured currency precision"
    );
    var converted = ReconciliationRules.Posting(
        new ReconciliationDocumentRow { Id = 1, Kind = "Customer payment", Currency = "USD", Total = 100, PostingCount = 1, PostedCount = 1, PostingCurrency = "AED", PostingTotal = 370 },
        2
    );
    Check(
        converted.Status == "Review" && converted.Actual == null && converted.Difference == null,
        "Converted settlements are reviewed without a false cross-currency difference"
    );
    await new SqlCommand($"CREATE DATABASE [{database}]", master).ExecuteNonQueryAsync();
    created = true;
    await using var sql = new SqlConnection(connection);
    await sql.OpenAsync();
    foreach (var schema in new[]
    {
        "expenses",
        "sales",
        "purchases",
        "accounting",
        "banking"
    }

    )
    {
        await new SqlCommand($"CREATE SCHEMA [{schema}]", sql).ExecuteNonQueryAsync();
    }

    await new SqlCommand(
            """
        CREATE TABLE expenses.Companies(Id int,Name nvarchar(160),Country nvarchar(2),Currency nvarchar(3));
        INSERT INTO expenses.Companies VALUES(1,'Company A','AE','AED'),(2,'Company B','IN','INR'),(3,'Empty company','AE','AED');
        CREATE TABLE sales.CurrencyMasters(CurrencyCode nvarchar(3),DecimalPlaces int);
        INSERT INTO sales.CurrencyMasters VALUES('AED',2),('INR',2),('USD',2);
        CREATE TABLE sales.BranchMasters(Name nvarchar(100),AccountingCompanyId int);
        INSERT INTO sales.BranchMasters VALUES('Office A',1),('Office B',2);
        CREATE TABLE sales.SalesInvoices(Id int,InvoiceNumber nvarchar(50),Branch nvarchar(100),Currency nvarchar(3),GrandTotal decimal(18,4),Status nvarchar(20),InvoiceDate date);
        INSERT INTO sales.SalesInvoices VALUES(1,'INV-POSTED','Office A','AED',100,'Issued','2026-10-01'),(2,'INV-MISSING','Office A','AED',50,'Issued','2026-10-03'),(3,'INV-FUTURE','Office A','AED',9,'Issued','2026-12-01'),(4,'INV-OTHER','Office B','INR',9999,'Issued','2026-10-01'),(5,'INV-DRAFT','Office A','AED',10,'Draft','2026-10-01');
        CREATE TABLE sales.SalesInvoicePayments(Id int,SalesInvoiceId int,Currency nvarchar(3),Amount decimal(18,4),PaymentDate date,Status nvarchar(20) NOT NULL DEFAULT 'Recorded');
        INSERT INTO sales.SalesInvoicePayments (Id,SalesInvoiceId,Currency,Amount,PaymentDate,Status) VALUES(99,1,'AED',999999,'2026-10-02','Voided');
        INSERT INTO sales.SalesInvoicePayments (Id,SalesInvoiceId,Currency,Amount,PaymentDate) VALUES(1,1,'AED',20,'2026-10-02');
        CREATE TABLE sales.SalesCreditNotes(Id int,SalesInvoiceId int,CreditNoteNumber nvarchar(50),Currency nvarchar(3),GrandTotal decimal(18,4),Status nvarchar(20),CreditNoteDate date);
        CREATE TABLE sales.CustomerCreditApplications(TargetInvoiceId int,Amount decimal(18,4),AppliedAtUtc datetime2);
        CREATE TABLE purchases.Bills(Id int,BillNumber nvarchar(50),Branch nvarchar(100),Currency nvarchar(3),GrandTotal decimal(18,4),Status nvarchar(20),BillDate date);
        INSERT INTO purchases.Bills VALUES(1,'BILL-POSTED','Office A','AED',60,'Issued','2026-10-01');
        CREATE TABLE purchases.PaymentsMade(Id int,BillId int,PaymentNumber nvarchar(50),Branch nvarchar(100),Currency nvarchar(3),Amount decimal(18,4),Status nvarchar(20),PaymentDate date);
        INSERT INTO purchases.PaymentsMade VALUES(1,1,'PM-1','Office A','AED',10,'Recorded','2026-10-02');
        CREATE TABLE purchases.VendorCredits(Id int,VendorCreditNumber nvarchar(50),Branch nvarchar(100),Currency nvarchar(3),GrandTotal decimal(18,4),Status nvarchar(20),CreditDate date);
        INSERT INTO purchases.VendorCredits VALUES(1,'VC-MISSING','Office A','AED',5,'Issued','2026-10-03');
        CREATE TABLE purchases.VendorCreditApplications(BillId int,Amount decimal(18,4),Status nvarchar(20),ApplicationDate date);
        CREATE TABLE expenses.Expenses(Id int,Reference nvarchar(100),CompanyId int,Currency nvarchar(3),Amount decimal(18,4),Status nvarchar(20),Date date);
        INSERT INTO expenses.Expenses VALUES(1,'EXP-MISSING',1,'AED',15,'Recorded','2026-10-03'),(2,'EXP-DRAFT',1,'AED',10,'Draft','2026-10-03');
        CREATE TABLE expenses.ExpenseClaims(Id int,Reference nvarchar(100),CompanyId int,Currency nvarchar(3),Amount decimal(18,4),Status nvarchar(20),ApprovedDate date,PaidDate date,PaymentAccountId int);
        INSERT INTO expenses.ExpenseClaims VALUES(1,'CLAIM-1',1,'AED',12,'Paid','2026-10-03','2026-10-04',7);
        CREATE TABLE accounting.Accounts(Id int,CompanyId int,Name nvarchar(160),Type nvarchar(20),IsPostingAccount bit,SystemKey nvarchar(60),ParentAccountId int);
        INSERT INTO accounting.Accounts VALUES(1,1,'AR','Asset',1,'ACCOUNTS_RECEIVABLE',NULL),(2,1,'AP','Liability',1,'ACCOUNTS_PAYABLE',NULL),(3,1,'Bank','Asset',1,NULL,4),(4,1,'Bank group','Asset',0,'BANK_ACCOUNTS',NULL),(5,1,'Expense','Expense',1,NULL,NULL),(6,1,'Employee payable','Liability',1,NULL,NULL),(7,1,'Equipment','Asset',1,NULL,NULL),(8,1,'Revenue','Income',1,NULL,NULL),(9,1,'Foreign bank','Asset',1,NULL,4),(10,2,'Wrong company','Asset',1,NULL,NULL),(11,1,'Equity','Equity',1,NULL,NULL);
        CREATE TABLE accounting.JournalEntries(Id int,CompanyId int,EntryNumber nvarchar(50),EntryDate date,Currency nvarchar(3),ExchangeRate decimal(18,8),TotalDebit decimal(18,4),TotalCredit decimal(18,4),Status nvarchar(20),Source nvarchar(30),SourceDocumentId nvarchar(100));
        INSERT INTO accounting.JournalEntries VALUES
            (1,1,'JE-INV','2026-10-01','AED',1,100,100,'Posted','SalesInvoice','1'),
            (2,1,'JE-RECEIPT','2026-10-02','AED',1,20,20,'Posted','PaymentReceived','1'),
            (3,1,'JE-BILL','2026-10-01','AED',1,60,60,'Posted','PurchaseBill','1'),
            (4,1,'JE-PAYMENT','2026-10-02','AED',1,10,10,'Posted','PaymentMade','1'),
            (5,1,'JE-CLAIM','2026-10-03','AED',1,12,12,'Posted','Expense','Claim:1:approval'),
            (6,1,'JE-CLAIM-PAID','2026-10-04','AED',1,12,12,'Posted','Expense','Claim:1:payment'),
            (7,1,'JE-OPENING','2026-10-01','AED',1,100,100,'Posted','OpeningBalance','1'),
            (8,1,'JE-BAD-LINES','2026-10-05','AED',1,5,5,'Posted','Manual','test');
        CREATE TABLE accounting.JournalEntryLines(Id int,JournalEntryId int,CompanyId int,AccountId int,DebitAmount decimal(18,4),CreditAmount decimal(18,4),BaseDebitAmount decimal(18,4),BaseCreditAmount decimal(18,4));
        INSERT INTO accounting.JournalEntryLines VALUES
            (1,1,1,1,100,0,100,0),(2,1,1,8,0,100,0,100),
            (3,2,1,3,20,0,20,0),(4,2,1,1,0,20,0,20),
            (5,3,1,5,60,0,60,0),(6,3,1,2,0,60,0,60),
            (7,4,1,2,10,0,10,0),(8,4,1,3,0,10,0,10),
            (9,5,1,5,12,0,12,0),(10,5,1,6,0,12,0,12),
            (11,6,1,6,12,0,12,0),(12,6,1,7,0,12,0,12),
            (13,7,1,3,100,0,100,0),(14,7,1,11,0,100,0,100),
            (15,8,2,5,4,0,4,0),(16,8,1,11,0,4,0,4);
        CREATE TABLE expenses.BankAccounts(Id int,CompanyId int,Name nvarchar(160),Currency nvarchar(3),AccountingAccountId int,OpeningBalance decimal(18,4),OpeningBalanceDate date);
        INSERT INTO expenses.BankAccounts VALUES(1,1,'Main bank','AED',3,100,'2026-10-01'),(2,1,'Foreign bank','USD',9,50,'2026-10-01'),(3,1,'Unmapped bank','AED',NULL,0,'2026-10-01'),(4,1,'Cross-company mapping','AED',10,0,'2026-10-01');
        CREATE TABLE banking.BankTransactions(Id int,BankAccountId int,CompanyId int,Status nvarchar(20),TransactionDate date,Direction int,Amount decimal(18,4));
        INSERT INTO banking.BankTransactions VALUES(1,1,1,'Posted','2026-10-02',1,20),(2,1,1,'Posted','2026-10-02',2,10),(3,1,1,'Draft','2026-10-02',2,100),(4,1,1,'Posted','2026-12-01',2,100);
        CREATE TABLE banking.BankReconciliations(Id int,BankAccountId int,CompanyId int,Status nvarchar(20),PeriodEnd date);
        INSERT INTO banking.BankReconciliations VALUES(1,1,1,'Completed','2026-10-31');
        CREATE TABLE banking.BankStatementLines(Id int,BankReconciliationId int,MatchedBankTransactionId int);
        INSERT INTO banking.BankStatementLines VALUES(1,1,1);
        """,
            sql
        ).ExecuteNonQueryAsync();
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
    start.ArgumentList.Add("http://127.0.0.1:5404");
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
        BaseAddress = new Uri("http://127.0.0.1:5404"),
        Timeout = TimeSpan.FromSeconds(20)
    };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            using var ping = await client.GetAsync("/api/reconciliation-reports");
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
            throw new Exception("Test server failed: " + output);
        }

        await Task.Delay(200);
    }

    async Task<JsonArray> Report(
        int company = 1,
        string date = "2026-10-31"
    )
    {
        using var response = await client.GetAsync($"/api/reconciliation-reports?companyId={company}&asOf={date}");
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(text + output);
        }

        return JsonNode.Parse(text)!["checks"]!.AsArray();
    }

    string Status(
        JsonArray rows,
        string id
    ) => rows.Single(row => row!["id"]!.GetValue<string>() == id)!["status"]!.GetValue<string>();
    var rows = await Report();
    Check(
        Status(rows, "posting-Sales invoice-1") == "Matched" && Status(rows, "posting-Sales invoice-2") == "Issue",
        "Posted invoices match; missing invoice journals are flagged"
    );
    Check(
        Status(rows, "posting-Recorded expense-1") == "Issue" && Status(rows, "posting-Vendor credit-1") == "Issue",
        "Missing standalone expense and vendor-credit postings are exposed"
    );
    Check(
        !rows.Any(row => row!["reference"]?.GetValue<string>() is "INV-FUTURE" or "INV-DRAFT" or "INV-OTHER" or "EXP-DRAFT"),
        "Future, draft and other-company source records are excluded"
    );
    Check(
        Status(rows, "trial-balance") == "Matched" && Status(rows, "journal-8") == "Issue",
        "A balanced trial balance does not hide header/line or ownership errors"
    );
    Check(
        Status(rows, "bank-1") == "Matched",
        "Bank opening balance and posted movements reconcile to the mapped ledger"
    );
    Check(
        Status(rows, "bank-2") == "Not comparable" && Status(rows, "bank-3") == "Issue" && Status(rows, "bank-4") == "Issue",
        "Foreign currency, missing mappings and cross-company mappings are distinguished"
    );
    Check(
        Status(rows, "claim-bank-1") == "Issue" && Status(rows, "posting-Claim reimbursement-1") == "Matched",
        "A posted reimbursement still flags an inappropriate payout account"
    );
    Check(
        rows.Single(row => row!["id"]!.GetValue<string>() == "control-Receivables")!["expected"] == null,
        "Missing booking rates do not become fabricated zero balances"
    );
    Check(
        Status(rows, "control-Payables") == "Matched",
        "Payable source balances match their control account"
    );
    Check(Status(rows, "statement-1") == "Matched", "Completed statement coverage uses the actual cutoff");
    await new SqlCommand("UPDATE expenses.BankAccounts SET OpeningBalance=120 WHERE Id=1", sql).ExecuteNonQueryAsync();
    Check(
        Status(await Report(), "bank-1") == "Review",
        "Opening-balance differences require review instead of an automatic correction"
    );
    await new SqlCommand("UPDATE expenses.BankAccounts SET AccountingAccountId=3 WHERE Id=3", sql).ExecuteNonQueryAsync();
    Check(Status(await Report(), "bank-1") == "Review", "Duplicate bank-to-ledger mappings are detected");
    Check(
        Status(await Report(3), "empty-coverage") == "Review",
        "An empty company is not presented as proof of completeness"
    );
    var earlier = await Report(1, "2026-10-01");
    Check(
        !earlier.Any(row => row!["reference"]?.GetValue<string>() == "EXP-MISSING"),
        "Earlier cutoff excludes later source records"
    );
    using (var future = await client.GetAsync("/api/reconciliation-reports?companyId=1&asOf=9999-12-31"))
        Check(future.StatusCode == HttpStatusCode.BadRequest, "Future cutoffs are rejected");
    using (var unknown = await client.GetAsync("/api/reconciliation-reports?companyId=999&asOf=2026-10-31"))
        Check(unknown.StatusCode == HttpStatusCode.NotFound, "Unknown companies are rejected");
    var sourceCount = (int)(await new SqlCommand("SELECT COUNT(*) FROM accounting.JournalEntries", sql).ExecuteScalarAsync())!;
    Check(sourceCount == 8, "Running checks does not create or repair journal entries");
    Console.WriteLine($"All {checks} reconciliation checks passed.");
}
finally
{
    if (server is { HasExited: false })
    {
        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
    }

    server?.Dispose();
    if (!database.StartsWith("OrbitReconTests_", StringComparison.Ordinal) || database.Length != 48)
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
