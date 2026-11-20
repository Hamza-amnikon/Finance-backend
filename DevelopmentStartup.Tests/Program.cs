using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.AspNetCore.Connections;
using SparkFinance.Http;

if (args.Contains("--host"))
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        EnvironmentName = Environments.Development,
        ContentRootPath = AppContext.BaseDirectory,
        ApplicationName = typeof(Program).Assembly.GetName().Name
    });
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    var app = builder.Build();
    app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine("CHECK_HOST_READY"));
    app.RunWithDevelopmentInstanceGuard();
    return;
}

Check(DevelopmentServiceStartup.IsAddressAlreadyInUse(new IOException("Bind failed", new AddressInUseException("Occupied"))),
    "Address-in-use errors are recognized through inner exceptions.");
Check(!DevelopmentServiceStartup.IsAddressAlreadyInUse(new IOException("Different error")),
    "Unrelated startup failures are not suppressed.");
var assembly = Assembly.GetExecutingAssembly().Location;
Process StartHost()
{
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    start.ArgumentList.Add(assembly);
    start.ArgumentList.Add("--host");
    return Process.Start(start)!;
}

using var first = StartHost();
var errors = first.StandardError.ReadToEndAsync();
try
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    while (true)
    {
        var line = await first.StandardOutput.ReadLineAsync(timeout.Token);
        if (line == "CHECK_HOST_READY")
        {
            break;
        }
        if (line == null)
        {
            throw new Exception("The isolated host did not start: " + await errors);
        }
    }
    using var duplicate = StartHost();
    var output = duplicate.StandardOutput.ReadToEndAsync();
    var duplicateErrors = duplicate.StandardError.ReadToEndAsync();
    await duplicate.WaitForExitAsync(timeout.Token);
    var result = await output;
    Check(duplicate.ExitCode == 0 && result.Contains("already running for this checkout"),
        "A second development host exits cleanly with a clear message.");
    Check(!result.Contains("CHECK_HOST_READY") && !first.HasExited,
        "The duplicate never starts and the first host remains running.");
    Check(!(await duplicateErrors).Contains("Unhandled exception"),
        "The duplicate does not cause an unhandled startup exception.");
}
finally
{
    if (!first.HasExited)
    {
        first.Kill(true);
        await first.WaitForExitAsync();
    }
}

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
var collisionBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Development,
    ContentRootPath = AppContext.BaseDirectory,
    ApplicationName = typeof(Program).Assembly.GetName().Name
});
collisionBuilder.Logging.ClearProviders();
collisionBuilder.WebHost.UseUrls("http://127.0.0.1:" + occupiedPort);
var collisionApp = collisionBuilder.Build();
collisionApp.RunWithDevelopmentInstanceGuard();
Check(Environment.ExitCode == 1, "A different listener owning the configured port fails cleanly with a nonzero status.");
Environment.ExitCode = 0;
Console.WriteLine("Development startup guard checks passed. No Finance services or databases were used.");

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
    Console.WriteLine("PASS: " + message);
}
