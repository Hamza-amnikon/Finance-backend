using BankingService.API.Data;
using BankingService.API.Models;
using BankingService.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var server = Environment.GetEnvironmentVariable("BANKING_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB";
var database = "FinanceBranchBankingChecks_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = database,
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    ConnectTimeout = 10
};
var options = new DbContextOptionsBuilder<BankingDbContext>().UseSqlServer(connection.ConnectionString).Options;
var clock = new CheckClock();
var settings = Options.Create(new PaymentBankingOptions
{
    Enabled = true,
    CashModes = ["Cash"],
    DraftModes = ["Cheque"],
    PostedModes = ["NEFT", "RTGS"],
    Routes = [
        new() { Branches = ["Dubai"], Currency = "AED", BankAccountName = "Dubai test bank", TimeZoneId = "Arabian Standard Time" },
        new() { Branches = ["Turbhe", "India", "Mumbai"], Currency = "INR", BankAccountName = "India test bank", TimeZoneId = "India Standard Time" }
    ]
});
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    checks++;
}
await using var context = new BankingDbContext(options);
var created = false;
try
{
    created = await context.Database.EnsureCreatedAsync();
    if (!created) throw new Exception("Refusing to use an existing test database.");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA purchases");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA sales");
    await context.Database.ExecuteSqlRawAsync("""
        CREATE TABLE purchases.PaymentsMade (
            Id int PRIMARY KEY, RequestId uniqueidentifier NOT NULL, PaymentNumber nvarchar(50) NOT NULL,
            VendorName nvarchar(200) NOT NULL, Branch nvarchar(20) NOT NULL, Currency nvarchar(3) NOT NULL,
            Amount decimal(18,4) NOT NULL, PaymentMode nvarchar(50) NOT NULL, Reference nvarchar(200) NOT NULL,
            PaymentDate date NOT NULL, CreatedDate datetime2 NOT NULL, VoidedDate datetime2 NULL,
            Status nvarchar(20) NOT NULL, BankTransactionId int NULL);
        CREATE TABLE sales.SalesInvoices (Id int PRIMARY KEY, ClientName nvarchar(200), Branch nvarchar(20));
        CREATE TABLE sales.SalesInvoicePayments (
            Id int PRIMARY KEY, SalesInvoiceId int, RequestId uniqueidentifier NOT NULL, PaymentDate date NOT NULL,
            Amount decimal(18,4) NOT NULL, Currency nvarchar(3) NOT NULL, PaymentMode nvarchar(50) NOT NULL,
            Reference nvarchar(200) NOT NULL, CreatedDate datetime2 NOT NULL, BankTransactionId int NULL);
        INSERT INTO sales.SalesInvoices VALUES (1,'Customer','Dubai');
        """);
    context.PaymentBankingSettings.Add(new PaymentBankingSettings { Id = 1, SourceAfterUtc = clock.GetUtcNow().UtcDateTime.AddHours(-1) });
    var dubai = new BankAccount { CompanyId = 10, Name = "Dubai test bank", Currency = "AED", CurrencyDecimalPlaces = 2, OpeningBalanceDate = new DateOnly(2026,10,1), OpeningBalance = 1000 };
    var india = new BankAccount { CompanyId = 20, Name = "India test bank", Currency = "INR", CurrencyDecimalPlaces = 2, OpeningBalanceDate = new DateOnly(2026,10,1), OpeningBalance = 1000 };
    context.BankAccounts.AddRange(dubai,india);
    await context.SaveChangesAsync();
    context.ChangeTracker.Clear();
    var service = new PaymentBankingService(context, settings, clock, NullLogger<PaymentBankingService>.Instance);

    async Task Made(int id, string branch, string currency, decimal amount, string mode = "RTGS", DateTime? createdAt = null, DateOnly? date = null)
    {
        var request = Guid.NewGuid();
        var number = "PM-TEST-" + id;
        var createdDate = createdAt ?? clock.GetUtcNow().UtcDateTime;
        var paymentDate = date ?? new DateOnly(2026,10,23);
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO purchases.PaymentsMade (Id,RequestId,PaymentNumber,VendorName,Branch,Currency,Amount,PaymentMode,Reference,PaymentDate,CreatedDate,Status) VALUES({id},{request},{number},'Vendor',{branch},{currency},{amount},{mode},'TEST',{paymentDate},{createdDate},'Recorded')");
    }
    async Task<PaymentBankingLink> Link(string source, int id) => await context.PaymentBankingLinks.AsNoTracking().SingleAsync(row => row.Source == source && row.SourcePaymentId == id);
    async Task<BankTransaction> Entry(string source,int id) => await context.BankTransactions.AsNoTracking().SingleAsync(row => row.Id == context.PaymentBankingLinks.Where(link => link.Source == source && link.SourcePaymentId == id).Select(link => link.BankTransactionId).Single());

    await Made(1,"Turbhe","INR",10);
    await Made(2,"Dubai","AED",25,"NEFT");
    await Made(3,"Mumbai","INR",7);
    await Made(4,"India","INR",6);
    await Made(5,"Turbhe","INR",5,"Cash");
    await Made(6,"Turbhe","INR",9,"Cheque");
    await Made(7,"Dubai","USD",15);
    await Made(8,"Unknown branch","INR",15);
    await Made(9,"Turbhe","INR",100,createdAt:clock.GetUtcNow().UtcDateTime.AddDays(-1));
    var customerRequest = Guid.NewGuid();
    var now = clock.GetUtcNow().UtcDateTime;
    await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sales.SalesInvoicePayments VALUES(1,1,{customerRequest},'2026-10-23',20,'AED','RTGS','TEST',{now},NULL)");
    await service.RunOnceAsync(default);

    var outEntry=await Entry("Made",1);
    Check(outEntry.Amount==10 && outEntry.BankAccountId==india.Id && outEntry.Direction==BankTransactionDirection.MoneyOut && outEntry.Status=="Posted","Partial payment posts only 10 INR to Indian bank");
    Check((await Entry("Made",2)).BankAccountId==dubai.Id && (await Entry("Made",2)).Currency=="AED","Dubai AED routes to Dubai bank");
    Check((await Entry("Made",3)).BankAccountId==india.Id,"Mumbai alias routes to Indian bank");
    Check((await Entry("Made",4)).BankAccountId==india.Id,"India alias routes to Indian bank");
    var incoming=await Entry("Received",1);
    Check(incoming.Amount==20 && incoming.BankAccountId==dubai.Id && incoming.Direction==BankTransactionDirection.MoneyIn,"Received payment creates matching Money In");
    Check((await Link("Made",5)).State=="NoBank" && (await Link("Made",5)).BankTransactionId==null,"Cash creates no bank transaction");
    Check((await Entry("Made",6)).Status=="Draft" && (await Entry("Made",6)).PostedDate==null,"Cheque stays Draft and does not affect posted balance");
    Check((await Link("Made",7)).State=="NeedsReview" && (await Link("Made",7)).BankTransactionId==null,"Currency mismatch never invents an exchange rate");
    Check((await Link("Made",8)).State=="NeedsReview","Unknown branch never chooses an arbitrary bank");
    Check(!await context.PaymentBankingLinks.AnyAsync(row=>row.Source=="Made" && row.SourcePaymentId==9),"Historical payments are not backfilled");
    var count=await context.BankTransactions.CountAsync();
    await service.RunOnceAsync(default);
    Check(await context.BankTransactions.CountAsync()==count,"Repeated processing never duplicates bank entries");
    var originalAmount=await context.Database.SqlQuery<decimal>($"SELECT Amount AS Value FROM purchases.PaymentsMade WHERE Id=1").SingleAsync();
    Check(originalAmount==10,"Original payment amount remains unchanged");
    var indiaNet=await context.BankTransactions.Where(row=>row.BankAccountId==india.Id && row.Status=="Posted").SumAsync(row=>row.Direction==BankTransactionDirection.MoneyIn ? row.Amount : -row.Amount);
    Check(indiaNet==-23,"Bank deduction excludes cash and uncleared cheque");

    // Close a period with no lines to exercise the automatic draft fallback.
    context.BankReconciliations.Add(new BankReconciliation
    {
        RequestId=Guid.NewGuid(),BankAccountId=india.Id,CompanyId=india.CompanyId,
        Currency="INR",CurrencyDecimalPlaces=2,PeriodStart=new DateOnly(2026,10,1),PeriodEnd=new DateOnly(2026,10,22),
        Status="Completed",CompletedDate=now
    });
    await context.SaveChangesAsync();
    context.ChangeTracker.Clear();
    await Made(10,"Turbhe","INR",11,date:new DateOnly(2026,10,22));
    await service.RunOnceAsync(default);
    Check((await Entry("Made",10)).Status=="Draft","Closed period creates Draft without blocking original payment");
    Check((await Entry("Made",10)).TransactionDate==new DateOnly(2026,10,22),"Original payment date is never moved to another day");

    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE purchases.PaymentsMade SET Status='Voided',VoidedDate={now} WHERE Id=1");
    await service.RunOnceAsync(default);
    var reversed=await Link("Made",1);
    var reverseEntry=await context.BankTransactions.AsNoTracking().SingleAsync(row=>row.Id==reversed.ReversalTransactionId);
    Check(reverseEntry.Direction==BankTransactionDirection.MoneyIn && reverseEntry.Amount==10 && reverseEntry.ReversalOfTransactionId==outEntry.Id,"Vendor void adds one matching reversal");
    Check((await context.BankTransactions.AsNoTracking().SingleAsync(row=>row.Id==outEntry.Id)).Status=="Posted","Original bank history is preserved");
    var reverseCount=await context.BankTransactions.CountAsync();
    await service.RunOnceAsync(default);
    Check(await context.BankTransactions.CountAsync()==reverseCount,"Repeated void processing does not reverse twice");
    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE purchases.PaymentsMade SET Status='Voided',VoidedDate={now} WHERE Id=6");
    await service.RunOnceAsync(default);
    Check((await Entry("Made",6)).Status=="Cancelled","Voiding an uncleared cheque cancels its draft");

    // Fixing a route retries the same payment instead of recreating its source record.
    settings.Value.Routes[1].Branches.Add("Unknown branch");
    await service.ProcessAsync("Made",8,default);
    context.ChangeTracker.Clear();
    Check((await Link("Made",8)).State=="Posted","Missing route can be repaired and safely retried");
    Console.WriteLine($"{checks} branch payment banking checks passed. No existing Finance data modified.");
}
finally
{
    SqlConnection.ClearAllPools();
    if(created && database.StartsWith("FinanceBranchBankingChecks_") && database.Length=="FinanceBranchBankingChecks_".Length+32)
    {
        connection.InitialCatalog="master";
        await using var master = new SqlConnection(connection.ConnectionString);
        await master.OpenAsync();
        await using var command=master.CreateCommand();
        command.CommandText=$"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
        await command.ExecuteNonQueryAsync();
    }
}

class CheckClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow()=>new DateTimeOffset(2026,10,23,12,0,0,TimeSpan.Zero);
}
