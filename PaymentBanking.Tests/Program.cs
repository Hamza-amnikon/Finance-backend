using Finance.Banking;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
void Reject(Action action, string name)
{
    try { action(); } catch (ArgumentException) { Check(true, name); return; }
    throw new Exception("FAILED: " + name);
}

PaymentBanking.ValidateSettlement("Cash", null, null);
Check(true, "Cash requires no bank account");
Reject(() => PaymentBanking.ValidateSettlement(null, null, null), "Missing settlement rejected");
Reject(() => PaymentBanking.ValidateSettlement("Bank", null, 10), "Missing account rejected");
Reject(() => PaymentBanking.ValidateSettlement("Cash", 1, 10), "Cash cannot carry bank details");
Reject(() => PaymentBanking.ValidateSettlement("Bank", 1, 0), "Zero bank amount rejected");
Reject(() => PaymentBanking.ValidateSettlement("Bank", 1, -1), "Negative bank amount rejected");
Reject(() => PaymentBanking.ValidateSettlement("Bank", 1, 100000000000000m), "SQL decimal overflow rejected");

if (args.Contains("--validation-only"))
{
    Console.WriteLine($"{passed} validation checks passed. SQL checks not requested.");
    return;
}

var databaseName = "FinancePaymentBankingChecks_" + Guid.NewGuid().ToString("N");
var connectionBuilder = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("PAYMENT_BANKING_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB",
    InitialCatalog = "master",
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    ConnectTimeout = 10
};

await using var master = new SqlConnection(connectionBuilder.ConnectionString);
try
{
    await master.OpenAsync();
}
catch (SqlException)
{
    Console.WriteLine($"{passed} validation checks passed. SQL integration checks BLOCKED: the test SQL Server is unavailable. No existing database was modified.");
    Environment.ExitCode = 2;
    return;
}

await using (var create = master.CreateCommand())
{
    create.CommandText = $"CREATE DATABASE [{databaseName}]";
    await create.ExecuteNonQueryAsync();
}
connectionBuilder.InitialCatalog = databaseName;
var options = new DbContextOptionsBuilder<CheckContext>().UseSqlServer(connectionBuilder.ConnectionString).Options;
CheckContext Context() => new CheckContext(options);
try
{
    await using var setup = Context();
    await setup.Database.ExecuteSqlRawAsync("CREATE SCHEMA expenses");
    await setup.Database.ExecuteSqlRawAsync("CREATE SCHEMA banking");
    await setup.Database.ExecuteSqlRawAsync("""
CREATE TABLE expenses.BankAccounts (Id int PRIMARY KEY, CompanyId int NOT NULL, Name nvarchar(160) NOT NULL, Currency nvarchar(3) NOT NULL, CurrencyDecimalPlaces int NOT NULL, OpeningBalanceDate date NULL, IsActive bit NOT NULL);
CREATE TABLE banking.BankReconciliations (Id int IDENTITY PRIMARY KEY, BankAccountId int NOT NULL, Status nvarchar(20) NOT NULL, PeriodEnd date NOT NULL);
CREATE INDEX IX_BankRecon_Account ON banking.BankReconciliations(BankAccountId,PeriodEnd);
CREATE TABLE banking.BankTransactions (Id int IDENTITY PRIMARY KEY, RequestId uniqueidentifier NOT NULL UNIQUE, RequestFingerprint nvarchar(64) NOT NULL, BankAccountId int NOT NULL, CompanyId int NOT NULL, TransactionDate date NOT NULL, Direction int NOT NULL, Amount decimal(18,4) NOT NULL, Currency nvarchar(3) NOT NULL, CurrencyDecimalPlaces int NOT NULL, Status nvarchar(20) NOT NULL, Reference nvarchar(200) NOT NULL, Description nvarchar(1000) NOT NULL, Notes nvarchar(2000) NOT NULL, ReversalOfTransactionId int NULL, CreatedDate datetime2 NOT NULL, PostedDate datetime2 NULL);
CREATE UNIQUE INDEX IX_BankReversal ON banking.BankTransactions(ReversalOfTransactionId) WHERE ReversalOfTransactionId IS NOT NULL;
CREATE TABLE PaymentRecords (Id int PRIMARY KEY);
INSERT INTO expenses.BankAccounts VALUES (1,10,'AED account','AED',2,'2026-10-01',1),(2,20,'INR account','INR',2,'2026-10-01',1),(3,30,'Four decimal account','TST',4,'2026-10-01',1),(4,40,'Inactive','INR',2,'2026-10-01',0);
""");
    async Task<PaymentBankPosting?> Post(CheckContext ctx, int account, decimal amount, string currency, decimal documentAmount, int direction = 2, DateOnly? date = null, Guid? request = null, int? reversal = null)
        => await PaymentBanking.PostAsync(ctx, "Bank", account, amount, currency, documentAmount, date ?? new DateOnly(2026,10,23), direction, request ?? Guid.NewGuid(), "TST", "Test payment", default, reversal);
    async Task ExpectRejected(Func<Task> action, string name)
    {
        try { await action(); } catch (ArgumentException) { Check(true,name); return; }
        throw new Exception("FAILED: " + name);
    }

    await using (var ctx = Context())
    await using (var tx = await ctx.Database.BeginTransactionAsync())
    {
        var p = await Post(ctx,1,85,"AED",85);
        var row = await ctx.Database.SqlQuery<Movement>($"SELECT Direction, Amount, Currency, CompanyId, Status FROM banking.BankTransactions WHERE Id={p!.Id}").SingleAsync();
        Check(row.Direction == 2 && row.Amount == 85 && row.Currency == "AED" && row.CompanyId == 10 && row.Status == "Posted", "Vendor payment posts Money Out to account's company");
        await tx.CommitAsync();
    }
    await using (var ctx = Context())
    await using (var tx = await ctx.Database.BeginTransactionAsync())
    {
        var p = await Post(ctx,2,2185.63m,"AED",85,1);
        var row = await ctx.Database.SqlQuery<Movement>($"SELECT Direction, Amount, Currency, CompanyId, Status FROM banking.BankTransactions WHERE Id={p!.Id}").SingleAsync();
        Check(row.Direction == 1 && row.Amount == 2185.63m && row.Currency == "INR" && row.CompanyId == 20, "Customer receipt posts actual converted Money In");
        await tx.CommitAsync();
    }
    await using (var ctx = Context())
    await using (var tx = await ctx.Database.BeginTransactionAsync())
    {
        var p = await Post(ctx,3,1.2345m,"TST",1.2345m);
        var amount = await ctx.Database.SqlQuery<decimal>($"SELECT Amount AS Value FROM banking.BankTransactions WHERE Id={p!.Id}").SingleAsync();
        Check(amount == 1.2345m,"Four-decimal bank amount preserved");
        await tx.RollbackAsync();
    }
    await using (var ctx = Context())
    await using (var tx = await ctx.Database.BeginTransactionAsync())
    {
        var countBefore = await ctx.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM banking.BankTransactions").SingleAsync();
        await ctx.Database.ExecuteSqlRawAsync("INSERT INTO PaymentRecords VALUES(1)");
        await Post(ctx,1,20,"AED",20);
        await tx.RollbackAsync();
        var countAfter = await ctx.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM banking.BankTransactions").SingleAsync();
        var records = await ctx.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM PaymentRecords").SingleAsync();
        Check(countBefore == countAfter && records == 0,"Rollback removes payment and bank entry together");
    }
    await using (var ctx = Context())
    await using (var tx = await ctx.Database.BeginTransactionAsync())
    {
        await ExpectRejected(async () => { await Post(ctx,1,86,"AED",85); }, "Same currency mismatch rejected");
        await ExpectRejected(async () => { await Post(ctx,1,1.111m,"AED",1.111m); }, "Account decimal precision enforced");
        await ExpectRejected(async () => { await Post(ctx,4,10,"INR",10); }, "Inactive account rejected");
        await ExpectRejected(async () => { await Post(ctx,999,10,"INR",10); }, "Unknown account rejected");
        await ExpectRejected(async () => { await Post(ctx,1,10,"AED",10,date:new DateOnly(2026,9,30)); }, "Before opening date rejected");
        var cash = await PaymentBanking.PostAsync(ctx,"Cash",null,null,"AED",10,new DateOnly(2026,10,23),2,Guid.NewGuid(),"","Cash test",default);
        Check(cash == null,"Cash skips ledger insertion");
        await tx.RollbackAsync();
    }
    await setup.Database.ExecuteSqlRawAsync("INSERT INTO banking.BankReconciliations(BankAccountId,Status,PeriodEnd) VALUES(1,'Completed','2026-10-23')");
    await using (var ctx = Context())
    await using (var tx = await ctx.Database.BeginTransactionAsync())
    {
        await ExpectRejected(async () => { await Post(ctx,1,10,"AED",10); },"Completed bank period rejects backdated payments");
        var reversal = await Post(ctx,1,85,"AED",85,1,new DateOnly(2026,10,24),reversal:1);
        var original = await ctx.Database.SqlQuery<string>($"SELECT Status AS Value FROM banking.BankTransactions WHERE Id=1").SingleAsync();
        Check(reversal != null && original == "Posted","Reversal appends opposite entry and preserves original");
        await tx.RollbackAsync();
    }
    Console.WriteLine($"{passed} checks passed against a disposable database. Existing Finance balances untouched.");
}
finally
{
    SqlConnection.ClearAllPools();
    if (!databaseName.StartsWith("FinancePaymentBankingChecks_") || databaseName.Length != 60)
        throw new Exception("Refusing to drop unexpected database name.");
    await using var drop = master.CreateCommand();
    drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
    await drop.ExecuteNonQueryAsync();
}

class CheckContext(DbContextOptions<CheckContext> options) : DbContext(options);
class Movement
{
    public int Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public int CompanyId { get; set; }
    public string Status { get; set; } = "";
}
