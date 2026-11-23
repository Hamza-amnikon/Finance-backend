using Microsoft.Data.SqlClient;

namespace PermissionService.API.Authentication
{
    public sealed class PermissionRequestFailureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PermissionRequestFailureMiddleware> _logger;

        public PermissionRequestFailureMiddleware(
            RequestDelegate next,
            ILogger<PermissionRequestFailureMiddleware> logger)
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
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = 499;
                }
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(exception, "A Permission Service operation timed out.");

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "The permission request timed out. Please refresh and retry.",
                    code = "permission_timeout"
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "The Permission Service database is unavailable.");

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "The permissions database is temporarily unavailable. Please retry.",
                    code = "permissions_database_unavailable"
                });
            }
        }
    }
}
