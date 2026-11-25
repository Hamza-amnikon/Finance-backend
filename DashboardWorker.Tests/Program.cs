using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using DashboardService.API.Services;
using Microsoft.Extensions.Logging.Abstractions;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAIL " + message);
    }
    checks++;
    Console.WriteLine("PASS " + message);
}
const string local = "Server=(localdb)\\MSSQLLocalDB;Database=WorkerTest;Integrated Security=True";
Check(LocalDbKeepAliveWorker.IsLocalDbConnection(local), "LocalDB connection is recognized");
Check(!LocalDbKeepAliveWorker.IsLocalDbConnection("Server=database;Database=Test;User ID=test;Password=test"),
    "Remote SQL Server does not start a LocalDB worker");
Check(!LocalDbKeepAliveWorker.IsLocalDbConnection("Server=database;Port=3306;Database=Test;User ID=test;Password=test"),
    "MySQL connection does not start a SQL Server keep-alive worker");
Check(!LocalDbKeepAliveWorker.IsLocalDbConnection("not a connection string"), "Invalid connection format does not escape from detection");

using var skipped = new TestWorker("Server=database;Port=3306;Database=Test;User ID=test;Password=test");
await skipped.StartAsync(CancellationToken.None);
await skipped.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
Check(skipped.ConnectionAttempts == 0 && skipped.ExecuteTask.IsCompletedSuccessfully,
    "Worker ignores non-LocalDB connections without attempting a database connection");

using var worker = new TestWorker(local);
await worker.StartAsync(CancellationToken.None);
await worker.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await worker.StopAsync(stopTimeout.Token);
await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
Check(worker.ExecuteTask.IsCompletedSuccessfully, "Stopping during a failed connection's retry wait completes without an escaping cancellation");
Check(worker.ConnectionAttempts == 1, "Shutdown does not open another connection");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Development
});
builder.Logging.ClearProviders();
builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
var hostedWorker = new TestWorker(local);
builder.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(hostedWorker);
await using (var app = builder.Build())
{
    await app.StartAsync();
    await hostedWorker.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Check(app.Lifetime.ApplicationStarted.IsCancellationRequested,
        "Host starts when its LocalDB worker encounters a connection failure");
    await app.StopAsync();
    Check(hostedWorker.ExecuteTask?.IsCompletedSuccessfully == true,
        "Host shutdown handles the worker's retry cancellation without an app.Run failure");
}
Console.WriteLine($"{checks} worker and host checks passed without contacting a database.");

sealed class TestWorker(string connectionString)
    : LocalDbKeepAliveWorker(connectionString, NullLogger<LocalDbKeepAliveWorker>.Instance)
{
    public int ConnectionAttempts { get; private set; }
    public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override DbConnection CreateConnection()
    {
        ConnectionAttempts++;
        Attempted.TrySetResult();
        throw new InvalidOperationException("Simulated unavailable LocalDB.");
    }
}
