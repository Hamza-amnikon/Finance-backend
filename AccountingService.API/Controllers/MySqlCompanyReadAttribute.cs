using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AccountingService.API.Controllers;

public sealed class MySqlCompanyReadAttribute : TypeFilterAttribute
{
    public MySqlCompanyReadAttribute(string permissionCode) : base(typeof(MySqlCompanyReadFilter))
    {
        Arguments = [permissionCode];
    }
}

public sealed class MySqlCompanyReadFilter(string permissionCode, IConfiguration configuration,
    IAccountingPageAccess companyAccess, ILogger<MySqlCompanyReadFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (configuration["Database:Provider"] != "MySql")
        {
            await next();
            return;
        }

        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            var access = await companyAccess.GetAsync(http.Request, permissionCode, http.RequestAborted);
            if (access.Status != 200)
            {
                context.Result = new StatusCodeResult(access.Status);
                return;
            }

            if (!int.TryParse(http.Request.Query["companyId"], out var companyId) || !access.Allows(companyId))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            context.Result = new StatusCodeResult(499);
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Accounting report company access could not be verified.");
            context.Result = new StatusCodeResult(503);
            return;
        }

        await next();
    }
}
