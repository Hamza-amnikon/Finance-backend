using ExchangeRateService.API.Data;
using ExchangeRateService.API.DTOs;
using ExchangeRateService.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var connection = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("EXCHANGE_RATE_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB",
    InitialCatalog = "FinanceRateAgentChecks_" + Guid.NewGuid().ToString("N"),
    IntegratedSecurity = true,
    TrustServerCertificate = true
};
var dbOptions = new DbContextOptionsBuilder<ExchangeRateDbContext>().UseSqlServer(connection.ConnectionString).Options;
await using var context = new ExchangeRateDbContext(dbOptions);
var created = false;
var checks = 0;
void Check(
    bool condition,
    string name
)
{
    if (!condition)
        throw new Exception("FAILED: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

try
{
    created = await context.Database.EnsureCreatedAsync();
    if (!created)
        throw new Exception("Refusing to use an existing database.");
    await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA sales");
    await context.Database.ExecuteSqlRawAsync("CREATE TABLE sales.CurrencyMasters (CurrencyCode nvarchar(3), IsActive bit); INSERT INTO sales.CurrencyMasters VALUES ('INR',1),('USD',1),('AED',1),('EUR',0)");
    var clock = new TestClock();
    var config = Options.Create(new DailyRateOptions());
    var provider = new TestProvider(clock);
    var agent = new DailyRateAgent(context, provider, config, clock);
    await agent.RunOnceAsync(default);
    Check(
        await context.DailyExchangeRates.CountAsync() == 3,
        "Only active currencies are saved, with INR included once"
    );
    Check(
        (await context.ExchangeRateChecks.SingleAsync()).Status == "Succeeded",
        "Successful daily check is recorded"
    );
    Check(provider.Dates.All(date => date == config.Value.ScheduledDate(clock.Now)),
        "Daily checks request their scheduled cutoff instead of undated latest rates");
    var calls = provider.Calls;
    await agent.RunOnceAsync(default);
    Check(provider.Calls == calls, "Repeated ticks do not refetch a successful scheduled day");
    await using (var restarted = new ExchangeRateDbContext(dbOptions))
        await new DailyRateAgent(restarted, provider, config, clock).RunOnceAsync(default);
    Check(provider.Calls == calls, "Restart reuses persisted daily completion");
    var stored = new StoredExchangeRateService(context, provider, config, clock);
    Check(
        (await stored.GetRateAsync("USD", "AED", null, default)).Rate == 3.6m,
        "Cross currency conversion uses one saved daily batch"
    );
    Check(
        (await stored.GetRateAsync("USD", "INR", null, default)).Rate == 90m,
        "Previous publication date remains usable after a successful check"
    );
    Check(
        (await stored.GetRateAsync("INR", "INR", null, default)).Rate == 1m,
        "Same currency stays exactly one"
    );
    var historical = await stored.GetRateAsync("USD", "INR", new DateOnly(2026, 10, 1), default);
    Check(
        historical.RateDate == new DateOnly(2026, 10, 1),
        "Historical dates before monitoring use the shared provider"
    );
    Check(
        config.Value.ScheduledDate(new DateTimeOffset(2026, 10, 28, 13, 59, 0, TimeSpan.Zero)) == new DateOnly(2026, 10, 27),
        "Daily boundary follows configured UTC hour"
    );
    clock.Now = clock.Now.AddDays(1);
    provider.FailUsd = true;
    await agent.RunOnceAsync(default);
    Check(
        (await context.ExchangeRateChecks.OrderByDescending(row => row.CheckDate).FirstAsync()).Status == "Partial",
        "Provider outage records partial status"
    );
    Check(
        await context.DailyExchangeRates.CountAsync(row => row.Currency == "USD") == 1,
        "Provider failure preserves the last successful rate"
    );
    try
    {
        await stored.GetRateAsync("USD", "INR", null, default);
        throw new Exception("Stale rate accepted");
    }
    catch (HttpRequestException)
    {
        Check(true, "Consumers cannot silently use an overdue rate");
    }

    calls = provider.Calls;
    await agent.RunOnceAsync(default);
    Check(provider.Calls == calls, "Failed checks respect the retry interval");
    clock.Now = clock.Now.AddHours(1);
    provider.FailUsd = false;
    await agent.RunOnceAsync(default);
    Check(
        await context.DailyExchangeRates.CountAsync() == 6,
        "Retry fills missing rows without duplicating the daily batch"
    );
    Check(
        (await context.ExchangeRateChecks.OrderByDescending(row => row.CheckDate).FirstAsync()).Status == "Succeeded",
        "Retry recovers to succeeded"
    );
    clock.Now = clock.Now.AddDays(1);
    provider.FutureDate = true;
    await agent.RunOnceAsync(default);
    var futureCheck = await context.ExchangeRateChecks.OrderByDescending(row => row.CheckDate).FirstAsync();
    Check(futureCheck.Status == "Partial" && futureCheck.Error!.Contains("publication date after"),
        "Future publication dates are rejected with an explanatory warning");
    Check(await context.DailyExchangeRates.CountAsync() == 6,
        "Rejected future rates do not overwrite or add saved snapshots");
    provider.FutureDate = false;
    calls = provider.Calls;
    config.Value.Enabled = false;
    clock.Now = clock.Now.AddDays(1);
    await agent.RunOnceAsync(default);
    Check(provider.Calls == calls, "Disabled agent makes no provider requests");
    Console.WriteLine($"{checks} exchange-rate agent checks passed.");
}
finally
{
    if (created)
        await context.Database.EnsureDeletedAsync();
}

sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

sealed class TestProvider(TestClock clock) : IDailyRateProvider
{
    public int Calls { get; private set; }
    public bool FailUsd { get; set; }
    public bool FutureDate { get; set; }
    public List<DateOnly?> Dates { get; } = [];

    public Task<ExchangeRateDto> GetRateAsync(
        string source,
        string target,
        DateOnly? date,
        CancellationToken cancellationToken
    )
    {
        Calls++;
        Dates.Add(date);
        if (FailUsd && source == "USD")
            throw new HttpRequestException("Test outage");
        var values = new Dictionary<string, decimal>
        {
            ["INR"] = 1m,
            ["USD"] = 90m,
            ["AED"] = 25m
        };
        return Task.FromResult(new ExchangeRateDto { Rate = values[source] / values[target], RateDate = FutureDate ? DateOnly.FromDateTime(clock.Now.UtcDateTime).AddDays(1) : date ?? DateOnly.FromDateTime(clock.Now.UtcDateTime).AddDays(-1), Source = "Test provider", RetrievedAtUtc = clock.Now });
    }
}
