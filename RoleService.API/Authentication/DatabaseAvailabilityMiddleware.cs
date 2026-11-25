using Microsoft.Data.SqlClient;
using SparkFinance.Http;
using MySql.Data.MySqlClient;

namespace RoleService.API.Authentication
{
    public sealed class DatabaseAvailabilityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DatabaseAvailabilityMiddleware> _logger;

        public DatabaseAvailabilityMiddleware(
            RequestDelegate next,
            ILogger<DatabaseAvailabilityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception) when (
                IsDatabaseFailure(exception) &&
                !RequestCancellation.IsCancellation(exception, context.RequestAborted))
            {
                _logger.LogError(exception, "Role Service could not verify database access.");
                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.RetryAfter = "5";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "The user and permission database is temporarily unavailable. Please retry. Your session has not been signed out.",
                    code = "database_unavailable"
                }, context.RequestAborted);
            }
        }

        public static bool IsDatabaseFailure(Exception exception)
        {
            if (exception is SqlException || exception is MySqlException)
            {
                return true;
            }

            if (exception is AggregateException aggregate)
            {
                return aggregate.InnerExceptions.Any(IsDatabaseFailure);
            }

            return exception.InnerException != null && IsDatabaseFailure(exception.InnerException);
        }
    }
}
