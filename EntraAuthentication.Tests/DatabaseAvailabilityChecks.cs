using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using RoleService.API.Authentication;

static class DatabaseAvailabilityChecks
{
    public static async Task RunAsync(IServiceProvider services)
    {
        SqlException databaseError;
        try
        {
            await using var connection = new SqlConnection(
                "Server=127.0.0.1,1;Database=UnavailableTest;User Id=test;Password=test;Connect Timeout=1;ConnectRetryCount=0;Encrypt=False");
            await connection.OpenAsync();
            throw new Exception("Expected the isolated invalid endpoint to fail.");
        }
        catch (SqlException exception)
        {
            databaseError = exception;
        }

        Check(DatabaseAvailabilityMiddleware.IsDatabaseFailure(databaseError),
            "SQL connection failures are recognized.");
        Check(DatabaseAvailabilityMiddleware.IsDatabaseFailure(new InvalidOperationException("Retry exhausted", databaseError)),
            "Wrapped SQL failures are recognized.");
        Check(!DatabaseAvailabilityMiddleware.IsDatabaseFailure(new InvalidOperationException("Application bug")),
            "Unrelated application errors are not hidden as database failures.");

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        var middleware = new DatabaseAvailabilityMiddleware(
            context => Task.FromException(databaseError),
            NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        await middleware.InvokeAsync(http);
        Check(http.Response.StatusCode == 503,
            "An unavailable database returns 503 instead of an authentication denial.");
        Check(!http.Response.Headers.ContainsKey("Set-Cookie"),
            "A database failure does not delete or replace the session cookie.");
        Check(http.Response.Headers.RetryAfter == "5" && http.Response.Headers.CacheControl == "no-store",
            "The failure response is retryable and cannot be cached.");
        http.Response.Body.Position = 0;
        var response = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Check(response.Contains("database_unavailable") && !response.Contains("127.0.0.1"),
            "The response explains the outage without exposing connection details.");

        var recovered = new DefaultHttpContext();
        var recovery = new DatabaseAvailabilityMiddleware(
            context =>
            {
                context.Response.StatusCode = 200;
                return Task.CompletedTask;
            }, NullLogger<DatabaseAvailabilityMiddleware>.Instance);
        await recovery.InvokeAsync(recovered);
        Check(recovered.Response.StatusCode == 200,
            "A recovered database request proceeds normally without a service restart.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception("FAILED: " + message);
        }
        Console.WriteLine("PASS: " + message);
    }
}
