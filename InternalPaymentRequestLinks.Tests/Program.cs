using System.Text.Json;
using BankingService.API.Data;
using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Repositories;
using BankingService.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var databaseName = "MeritInternalRequestLinkChecks_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"(localdb)\MSSQLLocalDB",
    InitialCatalog = databaseName,
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    ConnectTimeout = 15
};
var options = new DbContextOptionsBuilder<BankingDbContext>().UseSqlServer(connection.ConnectionString).Options;
var payer = new InternalPaymentRequestScope(false, [21], [], [21], [], "verified-payer");
var transferViewer = new InternalPaymentRequestScope(false, [21], [], [], [], "verified-payer");
var receiver = new InternalPaymentRequestScope(false, [7], [], [7], [], "receiver");
var admin = new InternalPaymentRequestScope(true, [], [], [], [], "admin");
var date = new DateOnly(2026, 11, 19);
await using var seed = new BankingDbContext(options);
var created = false;
try
{
    created = await seed.Database.EnsureCreatedAsync();
    Check(created, "The checks use a new isolated LocalDB database.");
    await seed.Database.ExecuteSqlRawAsync("CREATE TABLE expenses.Companies (Id int PRIMARY KEY, Name nvarchar(160) NOT NULL, Currency nvarchar(3) NOT NULL)");
    await seed.Database.ExecuteSqlRawAsync("INSERT expenses.Companies VALUES (7, 'Receiving office', 'INR'), (21, 'Paying office', 'AED')");
    var source = new BankAccount { CompanyId = 21, Name = "Source bank", Currency = "AED", CurrencyDecimalPlaces = 2, OpeningBalanceDate = date, AccountNumber = "source-test-number" };
    var destination = new BankAccount { CompanyId = 7, Name = "Receiving bank", Currency = "INR", CurrencyDecimalPlaces = 2, OpeningBalanceDate = date, AccountNumber = "receiving-test-number" };
    seed.BankAccounts.AddRange(source, destination);
    await seed.SaveChangesAsync();

    async Task<InternalPaymentRequest> Request(decimal amount = 100m, string status = "Approved")
    {
        var request = new InternalPaymentRequest
        {
            RequestId = Guid.NewGuid(), RequestingCompanyId = 7, PayingCompanyId = 21,
            ReceivingBankAccountId = destination.Id, Currency = "INR", CurrencyDecimalPlaces = 2,
            Amount = amount, Status = status, RequestDate = date, RequiredDate = date,
            CreatedBy = "test", Lines = [new() { Purpose = "Operating cost", Amount = amount }]
        };
        seed.InternalPaymentRequests.Add(request);
        await seed.SaveChangesAsync();
        return request;
    }

    async Task<BankTransfer> Transfer(decimal received)
    {
        var outgoing = new BankTransaction { RequestId = Guid.NewGuid(), BankAccountId = source.Id, CompanyId = 21, Currency = "AED", CurrencyDecimalPlaces = 2, Amount = 1m, Direction = BankTransactionDirection.MoneyOut, Status = "Posted", TransactionDate = date };
        var incoming = new BankTransaction { RequestId = Guid.NewGuid(), BankAccountId = destination.Id, CompanyId = 7, Currency = "INR", CurrencyDecimalPlaces = 2, Amount = received, Direction = BankTransactionDirection.MoneyIn, Status = "Posted", TransactionDate = date };
        seed.BankTransactions.AddRange(outgoing, incoming);
        await seed.SaveChangesAsync();
        var transfer = new BankTransfer
        {
            RequestId = Guid.NewGuid(), SourceBankAccountId = source.Id, SourceCompanyId = 21,
            DestinationBankAccountId = destination.Id, DestinationCompanyId = 7,
            SourceCurrency = "AED", SourceCurrencyDecimalPlaces = 2,
            DestinationCurrency = "INR", DestinationCurrencyDecimalPlaces = 2,
            SourceAmount = 1m, DestinationAmount = received, TransferDate = date, ReceivedDate = date,
            Status = BankTransferStatus.Completed, SourceTransactionId = outgoing.Id, DestinationTransactionId = incoming.Id
        };
        seed.BankTransfers.Add(transfer);
        await seed.SaveChangesAsync();
        return transfer;
    }

    async Task<object> Link(InternalPaymentRequest request, BankTransfer transfer, InternalPaymentRequestScope? scope = null, InternalPaymentRequestScope? transfers = null)
    {
        await using var context = new BankingDbContext(options);
        var service = new InternalPaymentRequestService(context, new InternalPaymentRequestRepository(context));
        return await service.LinkTransferAsync(request.Id, new InternalPaymentRequestTransferDto
        {
            BankTransferId = transfer.Id, RowVersion = request.RowVersion
        }, scope ?? payer, transfers ?? transferViewer, default);
    }

    async Task<InternalPaymentRequest> Read(int id)
    {
        await using var context = new BankingDbContext(options);
        return await new InternalPaymentRequestService(context, new InternalPaymentRequestRepository(context)).GetAsync(id, admin, default);
    }

    var request = await Request();
    var first = await Transfer(60m);
    var second = await Transfer(40m);
    var transferVersions = await seed.BankTransfers.AsNoTracking().ToDictionaryAsync(row => row.Id, row => Convert.ToBase64String(row.RowVersion));
    var transactionCount = await seed.BankTransactions.CountAsync();
    await Reject(() => Link(request, first, receiver), 403, "A receiving-company editor cannot link on behalf of the payer.");
    await Reject(() => Link(request, first, payer, receiver), 403, "Bank Transfers View permission is checked for the payer.");
    await Link(request, first);
    var partial = await Read(request.Id);
    Check(partial.ReceivedAmount == 60m && partial.RemainingAmount == 40m && partial.FundingStatus == "Partially Paid", "A partial link counts the actual destination receipt.");
    await Link(request, first);
    Check(await seed.InternalPaymentRequestTransfers.CountAsync() == 1, "Retrying the same link is idempotent even with the original version.");
    var otherRequest = await Request();
    await Reject(() => Link(otherRequest, first), 409, "One transfer cannot fund two requests.");
    await Reject(() => Link(request, second), 409, "A stale request version cannot create another link.");
    var excess = await Transfer(50m);
    await Reject(() => Link(partial, excess), 409, "A full transfer exceeding the remaining balance is rejected.");
    await Link(partial, second);
    var paid = await Read(request.Id);
    Check(paid.ReceivedAmount == 100m && paid.RemainingAmount == 0m && paid.FundingStatus == "Paid", "Multiple linked receipts settle the request exactly.");
    foreach (var transfer in await seed.BankTransfers.AsNoTracking().Where(row => transferVersions.Keys.Contains(row.Id)).ToListAsync())
    {
        Check(Convert.ToBase64String(transfer.RowVersion) == transferVersions[transfer.Id], "Linking leaves the bank-transfer row unchanged.");
    }
    Check(await seed.BankTransactions.CountAsync() == transactionCount + 2, "Linking creates no bank transactions; only the later test fixture adds two.");
    await using (var context = new BankingDbContext(options))
    {
        var service = new InternalPaymentRequestService(context, new InternalPaymentRequestRepository(context));
        await Reject(() => service.GetPrintableAsync(request.Id, payer, "Configured title", default), 403, "View/Edit permission does not grant print export.");
        var exported = await service.GetPrintableAsync(request.Id, payer with { ExportableCompanyIds = [21] }, "Configured title", default);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(exported));
        Check(json.RootElement.GetProperty("displayName").GetString() == "Configured title", "Printing uses the configured document title.");
        Check(json.RootElement.GetProperty("receivingBank").GetProperty("AccountNumber").GetString() == "receiving-test-number", "Authorized printing includes the stored receiving bank details.");
        await Reject(() => service.GetAsync(request.Id, new(false, [999], [], [], [], "other"), default), 404, "Unrelated companies cannot retrieve or print the request.");
    }

    var concurrent = await Request();
    var contenderA = await Transfer(60m);
    var contenderB = await Transfer(60m);
    async Task<bool> Attempt(InternalPaymentRequest target, BankTransfer transfer)
    {
        try
        {
            await Link(target, transfer);
            return true;
        }
        catch (InternalPaymentRequestException exception) when (exception.Status == 409)
        {
            return false;
        }
    }
    var attempts = await Task.WhenAll(Attempt(concurrent, contenderA), Attempt(concurrent, contenderB));
    Check(attempts.Count(success => success) == 1 && (await Read(concurrent.Id)).ReceivedAmount == 60m, "Concurrent links cannot overfund a request.");
    var targetA = await Request();
    var targetB = await Request();
    var sharedTransfer = await Transfer(25m);
    var competingRequests = await Task.WhenAll(Attempt(targetA, sharedTransfer), Attempt(targetB, sharedTransfer));
    Check(competingRequests.Count(success => success) == 1, "Concurrent requests cannot allocate the same transfer twice.");
    Check(await seed.InternalPaymentRequestTransfers.CountAsync(row => row.BankTransferId == sharedTransfer.Id) == 1, "The unique transfer link is enforced by the database.");
    await using (var transferContext = new BankingDbContext(options))
    {
        var service = new BankTransferService(
            new BankTransferRepository(transferContext),
            new BankAccountRepository(transferContext),
            new BankTransactionRepository(transferContext));
        var input = new CreateBankTransferDto
        {
            RequestId = Guid.NewGuid(),
            SourceBankAccountId = source.Id,
            DestinationBankAccountId = destination.Id,
            SourceAmount = 1m,
            SourceFeeAmount = 0m,
            TransferDate = date,
            Reference = "ORDINARY-UNLINKED"
        };
        var draft = await service.CreateAsync(input, default);
        Check(draft.Status == BankTransferStatus.Draft, "Ordinary transfer creation still produces a draft.");
        var retry = await service.CreateAsync(input, default);
        Check(retry.Id == draft.Id, "Ordinary transfer creation keeps its existing retry protection.");
        var sent = await service.ConfirmSentAsync(draft.Id, new ConfirmBankTransferSentDto
        {
            SourceAmount = 1m,
            SourceFeeAmount = 0m,
            TransferDate = date,
            Reference = "ORDINARY-UNLINKED",
            RowVersion = draft.RowVersion
        }, default);
        Check(sent?.Status == BankTransferStatus.Pending && sent.SourceTransactionId.HasValue,
            "Ordinary sent confirmation still posts the source debit.");
        var completed = await service.ConfirmReceivedAsync(draft.Id, new ConfirmBankTransferReceivedDto
        {
            DestinationAmount = 25.43m,
            ReceivedDate = date,
            RowVersion = sent!.RowVersion
        }, default);
        Check(completed?.Status == BankTransferStatus.Completed && completed.DestinationTransactionId.HasValue,
            "Ordinary receipt confirmation still posts the destination credit.");
        Check(!await transferContext.InternalPaymentRequestTransfers.AnyAsync(row => row.BankTransferId == draft.Id),
            "Ordinary transfers remain independent of requests unless explicitly linked.");
    }
    Console.WriteLine("Isolated SQL transfer-link and print permission checks passed.");
}
finally
{
    if (created && connection.InitialCatalog == databaseName && databaseName.StartsWith("MeritInternalRequestLinkChecks_", StringComparison.Ordinal))
    {
        SqlConnection.ClearAllPools();
        await seed.Database.EnsureDeletedAsync();
        Console.WriteLine("Removed the isolated test database.");
    }
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
    Console.WriteLine("PASS: " + message);
}

static async Task Reject(Func<Task> action, int status, string message)
{
    try
    {
        await action();
    }
    catch (InternalPaymentRequestException exception) when (exception.Status == status)
    {
        Console.WriteLine("PASS: " + message);
        return;
    }
    throw new Exception(message);
}
