using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AccountingService.API.Controllers
{
    public sealed class AccountsAccessFilter : IAsyncActionFilter
    {
        private readonly IAccountingPageAccess _access;
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<AccountsAccessFilter> _logger;

        public AccountsAccessFilter(
            IAccountingPageAccess access,
            IAntiforgery antiforgery,
            ILogger<AccountsAccessFilter> logger)
        {
            _access = access;
            _antiforgery = antiforgery;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            var action = context.ActionDescriptor.RouteValues["action"];
            var sharedCompanies = action == nameof(AccountsController.GetCompanies);
            var dashboardCompanies = action == nameof(AccountsController.DashboardCompanies);
            var companiesLookup = sharedCompanies || dashboardCompanies || action == nameof(AccountsController.ChartCompanies);
            var permissionCode = sharedCompanies ? null : dashboardCompanies ? "dashboard" : "accounting.chart-of-accounts";
            LedgerAccess access;

            try
            {
                access = await _access.GetAsync(
                    http.Request,
                    permissionCode,
                    http.RequestAborted);
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                context.Result = new StatusCodeResult(499);
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Chart of Accounts access verification failed.");
                context.Result = Error(503, "Access verification is unavailable. Please retry.");
                return;
            }

            if (access.Status != 200)
            {
                context.Result = Error(access.Status, "Sign in with an account authorized for Chart of Accounts.");
                return;
            }

            if (!companiesLookup)
            {
                var companyId = context.ActionArguments.Values.OfType<CreateAccountDto>().FirstOrDefault()?.CompanyId
                    ?? context.ActionArguments.Values.OfType<SetupChartOfAccountsDto>().FirstOrDefault()?.CompanyId
                    ?? (int.TryParse(http.Request.Query["companyId"], out var queryCompany) ? queryCompany : 0);
                var allowed = HttpMethods.IsGet(http.Request.Method)
                    ? access.Allows(companyId)
                    : HttpMethods.IsPost(http.Request.Method)
                        ? access.AllowsCreate(companyId)
                        : HttpMethods.IsPut(http.Request.Method) && access.AllowsEdit(companyId);

                if (!allowed)
                {
                    context.Result = Error(403, "You do not have permission for this Chart of Accounts action in this company.");
                    return;
                }

                if (!HttpMethods.IsGet(http.Request.Method) &&
                    string.IsNullOrWhiteSpace(http.Request.Headers.Authorization))
                {
                    try
                    {
                        await _antiforgery.ValidateRequestAsync(http);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        context.Result = Error(400, "Refresh the account form and retry with its CSRF token.");
                        return;
                    }
                }
            }

            http.Items[typeof(AccountsAccessFilter)] = access;
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
