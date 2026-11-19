using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SparkFinance.Http;

namespace DashboardService.API.Services
{
    public sealed class DashboardAccessFilter(
        DashboardAccessService accessService,
        ILogger<DashboardAccessFilter> logger) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";

            try
            {
                var (status, branches) = await accessService.GetAsync(http.Request, http.RequestAborted);
                if (status != 200)
                {
                    context.Result = Error(status);
                    return;
                }

                var branch = http.Request.Query["branch"].ToString().Trim();
                if (branch.Length != 0 && !branches.Any(item =>
                    string.Equals(item.BranchName, branch, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Result = Error(403);
                    return;
                }

                http.Items[typeof(DashboardAccessFilter)] = branches;
            }
            catch (Exception exception) when (RequestCancellation.IsCancellation(exception, http.RequestAborted))
            {
                context.Result = new StatusCodeResult(499);
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Dashboard access verification failed.");
                context.Result = Error(503);
                return;
            }

            await next();
        }

        private static ObjectResult Error(int status)
        {
            var message = status switch
            {
                401 => "Sign in to view the Dashboard.",
                403 => "You do not have Dashboard View access for this company.",
                _ => "Dashboard access could not be verified. Retry shortly."
            };

            return new ObjectResult(new { message }) { StatusCode = status };
        }
    }
}
