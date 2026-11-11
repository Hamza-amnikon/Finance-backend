using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using A = AccountingService.API.Models;
using AS = AccountingService.API.Services;
using AD = AccountingService.API.DTOs;
using B = BillService.API.Models;
using BD = BillService.API.DTOs;
using BS = BillService.API.Services;
using AccountingService.API.Data;
using AccountingService.API.Repositories;
using BillService.API.Data;
using BillService.API.Repositories;

var database = "BillCancelTests_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var billOptions = new DbContextOptionsBuilder<BillDbContext>().UseSqlServer(connection).Options;
var accountingOptions = new DbContextOptionsBuilder<AccountingDbContext>().UseSqlServer(connection).Options;
await using var bills = new BillDbContext(billOptions);
await using var accounting = new AccountingDbContext(accountingOptions);
var created = false;
var checks = 0;
void Check(bool value, string message)
{
    if (!value)
    {
        throw new Exception("FAIL: " + message);
    }
    checks++;
    Console.WriteLine("PASS: " + message);
}
AS.PurchaseBillReversalService Reversals(AccountingDbContext context)
{
    return new AS.PurchaseBillReversalService(context, new AS.JournalEntryService(new JournalEntryRepository(context)));
}
try
{
    created = await bills.Database.EnsureCreatedAsync();
    if (!created)
    {
        throw new Exception("Refusing to reuse an existing database.");
    }
    await bills.Database.ExecuteSqlRawAsync("CREATE SCHEMA expenses");
    await bills.Database.ExecuteSqlRawAsync("""
        CREATE TABLE expenses.Companies(Id int PRIMARY KEY,Name nvarchar(160),Country nvarchar(2),Currency nvarchar(3));
        INSERT INTO expenses.Companies VALUES(1,'Test company','AE','AED');
        CREATE TABLE purchases.PaymentsMade(Id int IDENTITY PRIMARY KEY,BillId int,Status nvarchar(20),Amount decimal(18,4));
        CREATE TABLE purchases.VendorCreditApplications(Id int IDENTITY PRIMARY KEY,BillId int,Status nvarchar(20),Amount decimal(18,4));
        """);
    foreach (var batch in Regex.Split(accounting.Database.GenerateCreateScript(), @"^GO\s*$", RegexOptions.Multiline))
    {
        if (!string.IsNullOrWhiteSpace(batch))
        {
            await accounting.Database.ExecuteSqlRawAsync(batch);
        }
    }
    var expense = new A.Account { CompanyId = 1, Code = "5290", Name = "Test expense", Type = A.AccountType.Expense,
        NormalBalance = A.AccountNormalBalance.Debit, IsActive = true, IsPostingAccount = true };
    var payable = new A.Account { CompanyId = 1, Code = "2110", Name = "Test payable", Type = A.AccountType.Liability,
        NormalBalance = A.AccountNormalBalance.Credit, IsActive = true, IsPostingAccount = true, SystemKey = "ACCOUNTS_PAYABLE", IsSystemAccount = true };
    accounting.Accounts.AddRange(expense, payable);
    await accounting.SaveChangesAsync();
    var adapter = new AccountingAdapter(accountingOptions);
    BS.BillService Service(BillDbContext context)
    {
        return new BS.BillService(context, new BillRepository(context), new BS.BillCalculator(), adapter, null!, new BS.BillTaxSettingsService(context));
    }
    async Task<B.Bill> AddBill(bool issued = true)
    {
        var bill = new B.Bill
        {
            RequestId = Guid.NewGuid(), VendorId = Guid.NewGuid(), VendorName = "Test vendor", Branch = "Dubai", Currency = "USD",
            BillDate = new DateOnly(2026, 11, 10), DueDate = new DateOnly(2026, 11, 11),
            BillNumber = issued ? "TEST-" + Guid.NewGuid().ToString("N")[..8] : null,
            Status = issued ? B.BillStatus.Issued : B.BillStatus.Draft,
            SubTotal = 100, GrandTotal = 100,
            Items = [new B.BillItem { LineNumber = 1, ItemName = "Test", AccountCode = "5290", Quantity = 1, UnitPrice = 100, LineSubTotal = 100, LineTotal = 100 }]
        };
        bills.Bills.Add(bill);
        await bills.SaveChangesAsync();
        return bill;
    }
    async Task<A.JournalEntry> Post(B.Bill bill)
    {
        var posting = new AS.PurchaseBillPostingService(new JournalEntryRepository(accounting), accounting);
        var result = await posting.PostAsync(new AD.PostPurchaseBillDto
        {
            CompanyId = 1, BillId = bill.Id, BillNumber = bill.BillNumber!, BillDate = bill.BillDate,
            Branch = "Dubai", Currency = "USD", ExchangeRate = 3.67m,
            VendorId = bill.VendorId, VendorName = bill.VendorName, NetAmount = 100, TotalAmount = 100,
            Expenses = [new AD.PurchaseBillExpensePostingDto { AccountCode = "5290", Description = "Test expense", Amount = 100 }]
        }, default);
        return await accounting.JournalEntries.Include(row => row.Lines).SingleAsync(row => row.Id == result.JournalEntryId);
    }
    async Task Reject(Func<Task> action, string message)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is ArgumentException || exception is ValidationException || exception is InvalidOperationException)
        {
            Check(true, message);
            return;
        }
        throw new Exception("FAIL: " + message);
    }
    var bill = await AddBill();
    var original = await Post(bill);
    await Reject(() => Reversals(accounting).ReverseAsync(bill.Id, default), "Accounting rejects reversal before source bill cancellation");
    bills.ChangeTracker.Clear();
    var cancelled = await Service(bills).ChangeStatusAsync(bill.Id, B.BillStatus.Cancelled, bill.RowVersion, default);
    Check(cancelled?.Status == B.BillStatus.Cancelled && !cancelled.AccountingReversalPending,
        "Cancelling an issued bill completes its accounting reversal");
    accounting.ChangeTracker.Clear();
    var reversed = await accounting.JournalEntries.SingleAsync(row => row.Id == original.Id);
    var reversal = await accounting.JournalEntries.Include(row => row.Lines).SingleAsync(row => row.ReversalOfJournalEntryId == original.Id);
    Check(reversed.Status == A.JournalEntryStatus.Reversed && reversal.Status == A.JournalEntryStatus.Posted,
        "Original journal is reversed and linked reversal is posted");
    Check(reversal.ExchangeRate == 3.67m && reversal.Lines.Sum(row => row.BaseDebitAmount) == 367 &&
        reversal.Lines.Sum(row => row.BaseCreditAmount) == 367,
        "Reversal preserves original exchange rate and base amounts");
    await Service(bills).ChangeStatusAsync(bill.Id, B.BillStatus.Cancelled, cancelled!.RowVersion, default);
    await Reversals(accounting).ReverseAsync(bill.Id, default);
    Check(await accounting.JournalEntries.CountAsync(row => row.ReversalOfJournalEntryId == original.Id) == 1,
        "Repeated cancellation and reversal requests do not duplicate the journal");
    var failedBill = await AddBill();
    var failedOriginal = await Post(failedBill);
    adapter.Fail = true;
    bills.ChangeTracker.Clear();
    try
    {
        await Service(bills).ChangeStatusAsync(failedBill.Id, B.BillStatus.Cancelled, failedBill.RowVersion, default);
        throw new Exception("FAIL: Simulated accounting outage was ignored");
    }
    catch (HttpRequestException)
    {
        bills.ChangeTracker.Clear();
        var pending = await bills.Bills.SingleAsync(row => row.Id == failedBill.Id);
        Check(pending.Status == B.BillStatus.Cancelled && pending.AccountingReversalPending,
            "Accounting failure leaves durable cancellation pending work");
        adapter.Fail = false;
        bills.ChangeTracker.Clear();
        await Service(bills).ChangeStatusAsync(pending.Id, B.BillStatus.Cancelled, pending.RowVersion, default);
        Check(!await bills.Bills.Where(row => row.Id == pending.Id).Select(row => row.AccountingReversalPending).SingleAsync(),
            "Retry clears the durable pending flag after reversal");
    }
    var lostReply = await AddBill();
    var lostOriginal = await Post(lostReply);
    adapter.LoseReply = true;
    bills.ChangeTracker.Clear();
    try
    {
        await Service(bills).ChangeStatusAsync(lostReply.Id, B.BillStatus.Cancelled, lostReply.RowVersion, default);
        throw new Exception("FAIL: Lost reply simulation was ignored");
    }
    catch (HttpRequestException)
    {
        bills.ChangeTracker.Clear();
        var pending = await bills.Bills.SingleAsync(row => row.Id == lostReply.Id);
        adapter.LoseReply = false;
        bills.ChangeTracker.Clear();
        await Service(bills).ChangeStatusAsync(pending.Id, B.BillStatus.Cancelled, pending.RowVersion, default);
        Check(await accounting.JournalEntries.CountAsync(row => row.ReversalOfJournalEntryId == lostOriginal.Id) == 1,
            "Retry after a lost response recovers the existing reversal");
    }
    var unpaidPosting = await AddBill();
    bills.ChangeTracker.Clear();
    var noPosting = await Service(bills).ChangeStatusAsync(unpaidPosting.Id, B.BillStatus.Cancelled, unpaidPosting.RowVersion, default);
    Check(noPosting?.AccountingReversalPending == false, "An issued bill without a saved posting can cancel without an invented journal");
    await Reject(() => Post(unpaidPosting), "Delayed posting cannot recreate a cancelled bill's accounting entry");
    var draft = await AddBill(false);
    var calls = adapter.Calls;
    bills.ChangeTracker.Clear();
    await Service(bills).ChangeStatusAsync(draft.Id, B.BillStatus.Cancelled, draft.RowVersion, default);
    Check(adapter.Calls == calls, "Draft cancellation does not call accounting");
    var paid = await AddBill();
    await bills.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO purchases.PaymentsMade(BillId,Status,Amount) VALUES({paid.Id},'Recorded',1)");
    bills.ChangeTracker.Clear();
    await Reject(() => Service(bills).ChangeStatusAsync(paid.Id, B.BillStatus.Cancelled, paid.RowVersion, default),
        "Active payments still prevent bill cancellation");
    var credited = await AddBill();
    await bills.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO purchases.VendorCreditApplications(BillId,Status,Amount) VALUES({credited.Id},'Applied',1)");
    bills.ChangeTracker.Clear();
    await Reject(() => Service(bills).ChangeStatusAsync(credited.Id, B.BillStatus.Cancelled, credited.RowVersion, default),
        "Active credits still prevent bill cancellation");
    var concurrent = await AddBill();
    var concurrentOriginal = await Post(concurrent);
    await bills.Database.ExecuteSqlInterpolatedAsync($"UPDATE purchases.Bills SET Status='Cancelled',AccountingReversalPending=1 WHERE Id={concurrent.Id}");
    async Task ReverseSeparately()
    {
        await using var context = new AccountingDbContext(accountingOptions);
        await Reversals(context).ReverseAsync(concurrent.Id, default);
    }
    await Task.WhenAll(ReverseSeparately(), ReverseSeparately());
    Check(await accounting.JournalEntries.CountAsync(row => row.ReversalOfJournalEntryId == concurrentOriginal.Id) == 1,
        "Concurrent reversal requests create only one reversal");
    var workerBill = await AddBill();
    var workerOriginal = await Post(workerBill);
    adapter.Fail = true;
    bills.ChangeTracker.Clear();
    try
    {
        await Service(bills).ChangeStatusAsync(workerBill.Id, B.BillStatus.Cancelled, workerBill.RowVersion, default);
        throw new Exception("FAIL: The worker fixture did not remain pending");
    }
    catch (HttpRequestException)
    {
        adapter.Fail = false;
    }
    var services = new ServiceCollection();
    services.AddDbContext<BillDbContext>(options => options.UseSqlServer(connection));
    services.AddScoped<BS.IBillService>(provider => Service(provider.GetRequiredService<BillDbContext>()));
    using var provider = services.BuildServiceProvider();
    var worker = new BS.BillReversalWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BS.BillReversalWorker>.Instance);
    await worker.ProcessPendingAsync(default);
    Check(!await bills.Bills.Where(row => row.Id == workerBill.Id).Select(row => row.AccountingReversalPending).SingleAsync() &&
        await accounting.JournalEntries.CountAsync(row => row.ReversalOfJournalEntryId == workerOriginal.Id) == 1,
        "The automatic retry cycle completes durable work after accounting recovers");
    var manuallyReversedBill = await AddBill();
    var manuallyReversedOriginal = await Post(manuallyReversedBill);
    await new AS.JournalEntryService(new JournalEntryRepository(accounting)).ReverseAsync(
        manuallyReversedOriginal.Id, 1, new AD.JournalEntryActionDto
        {
            RowVersion = Convert.ToBase64String(manuallyReversedOriginal.RowVersion), Reason = "Test manual correction"
        }, default);
    bills.ChangeTracker.Clear();
    await Service(bills).ChangeStatusAsync(manuallyReversedBill.Id, B.BillStatus.Cancelled, manuallyReversedBill.RowVersion, default);
    Check(await accounting.JournalEntries.CountAsync(row => row.ReversalOfJournalEntryId == manuallyReversedOriginal.Id) == 1,
        "Cancellation reuses an existing manual reversal rather than duplicating it");
    Console.WriteLine($"All {checks} bill cancellation integration checks passed.");
}
finally
{
    if (created && database.StartsWith("BillCancelTests_") && database.Length == "BillCancelTests_".Length + 32)
    {
        await bills.Database.EnsureDeletedAsync();
    }
}

sealed class AccountingAdapter : BS.IBillAccountingApiService
{
    private readonly DbContextOptions<AccountingDbContext> _options;
    public bool Fail { get; set; }
    public bool LoseReply { get; set; }
    public int Calls { get; private set; }

    public AccountingAdapter(DbContextOptions<AccountingDbContext> options)
    {
        _options = options;
    }
    public Task<BD.BillAccountingCompanyDto?> GetCompanyAsync(int id, CancellationToken token)
    {
        throw new NotSupportedException();
    }
    public Task<BD.BillAccountingResultDto> PostBillAsync(BD.BillAccountingPostingDto dto, CancellationToken token)
    {
        throw new NotSupportedException();
    }
    public async Task<BD.BillAccountingResultDto> ReverseBillAsync(int billId, CancellationToken token)
    {
        Calls++;
        if (Fail)
        {
            throw new HttpRequestException("Simulated accounting outage");
        }
        await using var context = new AccountingDbContext(_options);
        var service = new AS.PurchaseBillReversalService(context, new AS.JournalEntryService(new JournalEntryRepository(context)));
        await service.ReverseAsync(billId, token);
        if (LoseReply)
        {
            throw new HttpRequestException("Simulated response loss after accounting committed");
        }
        return new BD.BillAccountingResultDto { Status = "Reversed" };
    }
}
