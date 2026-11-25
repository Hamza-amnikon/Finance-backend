using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AccountingService.API.Controllers
{
    public sealed class TaxSettingsAccessFilter : IAsyncActionFilter
    {
        private readonly IAccountingPageAccess _access;
        private readonly ILogger<TaxSettingsAccessFilter> _logger;

        public TaxSettingsAccessFilter(
            IAccountingPageAccess access,
            ILogger<TaxSettingsAccessFilter> logger)
        {
            _access = access;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";

            LedgerAccess permission;
            try
            {
                permission = await _access.GetAsync(
                    http.Request,
                    "settings.tax",
                    http.RequestAborted);
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                context.Result = new StatusCodeResult(499);
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Tax Settings access verification failed.");
                context.Result = Error(503, "Access verification is unavailable. Please retry.");
                return;
            }

            if (permission.Status != 200)
            {
                context.Result = Error(permission.Status, "Sign in to access Tax Settings.");
                return;
            }

            var companyId = context.ActionArguments.Values
                .OfType<TaxConfigurationDto>()
                .Select(value => value.CompanyId)
                .Concat(context.ActionArguments.Values
                    .OfType<GeneralVatSetupRequest>()
                    .Select(value => value.CompanyId))
                .FirstOrDefault();

            if (companyId == 0 &&
                int.TryParse(http.Request.Query["companyId"], out var queryCompanyId))
            {
                companyId = queryCompanyId;
            }

            var allowed = companyId > 0
                ? HttpMethods.IsGet(http.Request.Method)
                    ? permission.Allows(companyId)
                    : HttpMethods.IsPost(http.Request.Method)
                        ? permission.AllowsCreate(companyId)
                        : HttpMethods.IsPut(http.Request.Method) && permission.AllowsEdit(companyId)
                : HttpMethods.IsGet(http.Request.Method) &&
                    (permission.IsAdmin || permission.CompanyIds.Count > 0);

            if (!allowed)
            {
                context.Result = Error(403, "You do not have this Tax Settings action for the selected company.");
                return;
            }

            http.Items[typeof(TaxSettingsAccessFilter)] = permission;
            await next();
        }

        private static ObjectResult Error(int status, string message)
        {
            return new ObjectResult(new { message })
            {
                StatusCode = status
            };
        }
    }
}
