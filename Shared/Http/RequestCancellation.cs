using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace SparkFinance.Http
{
    internal static class RequestCancellation
    {
        public static bool IsCancellation(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (exception is OperationCanceledException)
            {
                return true;
            }

            // SqlClient can report a cancelled command as SQL error zero.
            // Timeouts and other SQL errors must keep their normal error handling.
            return exception is SqlException sqlException &&
                sqlException.Errors.Count > 0 &&
                sqlException.Errors.Cast<SqlError>().All(error => error.Number == 0);
        }

        public static IApplicationBuilder UseFinanceRequestCancellation(
            this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception exception)
                    when (IsCancellation(exception, context.RequestAborted))
                {
                    if (!context.Response.HasStarted)
                    {
                        context.Response.Clear();
                        context.Response.StatusCode = 499;
                    }
                }
            });
        }
    }
}
