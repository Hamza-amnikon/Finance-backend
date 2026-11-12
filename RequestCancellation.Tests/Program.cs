using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using SparkFinance.Http;

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();

var sqlCancellation = CreateSqlException(0);
var sqlTimeout = CreateSqlException(-2);
var sqlFailure = CreateSqlException(208);

await DocumentRequestChecks.RunAsync(sqlCancellation, sqlFailure, cancelled.Token);

await CheckHandledAsync(new TaskCanceledException(), cancelled.Token);
await CheckHandledAsync(sqlCancellation, cancelled.Token);
await CheckPropagatesAsync(sqlCancellation, CancellationToken.None);
await CheckPropagatesAsync(sqlTimeout, cancelled.Token);
await CheckPropagatesAsync(sqlFailure, cancelled.Token);
await CheckPropagatesAsync(new TaskCanceledException(), CancellationToken.None);
await CheckPropagatesAsync(new InvalidOperationException("Unexpected failure."), cancelled.Token);

using var services = new ServiceCollection().BuildServiceProvider();
var app = new ApplicationBuilder(services);
app.UseFinanceRequestCancellation();
app.Run(context =>
{
    context.Response.StatusCode = StatusCodes.Status200OK;
    return Task.CompletedTask;
});
var successfulContext = new DefaultHttpContext();
await app.Build()(successfulContext);
Check(successfulContext.Response.StatusCode == 200, "Successful requests keep their original response.");

if (args.Contains("--sql"))
{
    // Only read-only commands run against the existing Finance database.
    var connectionString = Environment.GetEnvironmentVariable("FINANCE_CANCELLATION_TEST_CONNECTION")
        ?? throw new InvalidOperationException("The SQL test connection was not supplied.");

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    using var queryCancellation = new CancellationTokenSource();
    await using var command = connection.CreateCommand();
    command.CommandText = "WAITFOR DELAY '00:00:05'; SELECT 1;";
    queryCancellation.CancelAfter(TimeSpan.FromMilliseconds(200));

    var observedCancellation = false;

    try
    {
        await command.ExecuteScalarAsync(queryCancellation.Token);
    }
    catch (Exception exception)
        when (RequestCancellation.IsCancellation(exception, queryCancellation.Token))
    {
        observedCancellation = true;
        Console.WriteLine("SQL cancellation type: " + exception.GetType().Name);
    }

    Check(observedCancellation, "A real cancelled SQL command is recognized.");

    await using var recovery = connection.CreateCommand();
    recovery.CommandText = "SELECT 1;";
    var value = await recovery.ExecuteScalarAsync();
    Check(Convert.ToInt32(value) == 1, "The connection accepts a successful query after cancellation.");
}

Console.WriteLine("Request cancellation checks passed.");

static async Task CheckHandledAsync(Exception exception, CancellationToken cancellationToken)
{
    var context = new DefaultHttpContext
    {
        RequestAborted = cancellationToken
    };
    context.Response.Headers.ContentLength = 100;

    await Pipeline(exception)(context);

    Check(context.Response.StatusCode == 499, exception.GetType().Name + " with a cancelled request returns 499.");
    Check(context.Response.ContentLength == null, "Cancelled responses clear unfinished content headers.");
}

static async Task CheckPropagatesAsync(Exception exception, CancellationToken cancellationToken)
{
    var context = new DefaultHttpContext
    {
        RequestAborted = cancellationToken
    };

    try
    {
        await Pipeline(exception)(context);
    }
    catch (Exception actual)
        when (ReferenceEquals(actual, exception))
    {
        Console.WriteLine("PASS: " + exception.GetType().Name + " remains visible when it is not request cancellation.");
        return;
    }

    throw new InvalidOperationException("A genuine error was hidden as request cancellation.");
}

static RequestDelegate Pipeline(Exception exception)
{
    var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
    app.UseFinanceRequestCancellation();
    app.Run(context => Task.FromException(exception));
    return app.Build();
}

static SqlException CreateSqlException(int number)
{
    // SqlClient has no public constructors for error responses.
    var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
        .Single(value =>
            value.GetParameters().Length == 9 &&
            value.GetParameters()[7].ParameterType == typeof(int));
    var error = (SqlError)constructor.Invoke(new object?[]
    {
        number,
        (byte)0,
        (byte)11,
        "Test server",
        "Test SQL error",
        "Test procedure",
        1,
        0,
        null
    });
    var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
    typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(errors, [error]);
    var factory = typeof(SqlException).GetMethod(
        "CreateException",
        BindingFlags.NonPublic | BindingFlags.Static,
        null,
        [typeof(SqlErrorCollection), typeof(string)],
        null)!;

    return (SqlException)factory.Invoke(null, [errors, "16.0"])!;
}

static void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException("FAIL: " + description);
    }

    Console.WriteLine("PASS: " + description);
}
