using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AccountingService.API.Controllers
{
    public sealed class GeneralLedgerAccessFilter : IAsyncActionFilter
    {
        private readonly IGeneralLedgerAccess _access;
        private readonly ILogger<GeneralLedgerAccessFilter> _logger;

        public GeneralLedgerAccessFilter(
            IGeneralLedgerAccess access,
            ILogger<GeneralLedgerAccessFilter> logger)
        {
            _access = access;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            LedgerAccess access;
            try
            {
                access = await _access.GetAsync(context.HttpContext.Request, context.HttpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                context.Result = new StatusCodeResult(499);
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "General Ledger access could not be verified.");
                context.Result = new ObjectResult(new { message = "Access verification is unavailable. Try again shortly." })
                {
                    StatusCode = 503
                };
                return;
            }

            if (access.Status != 200)
            {
                context.Result = new ObjectResult(new { message = "Sign in with an account authorized for General Ledger." })
                {
                    StatusCode = access.Status
                };
                return;
            }

            // The companies lookup is filtered in the action. Every other ledger endpoint needs a company.
            var companiesLookup = context.ActionDescriptor.RouteValues["action"] == "Companies";
            if (!companiesLookup &&
                (!int.TryParse(context.HttpContext.Request.Query["companyId"], out var companyId) || !access.Allows(companyId)))
            {
                context.Result = new ObjectResult(new { message = "You do not have General Ledger access for this company." })
                {
                    StatusCode = 403
                };
                return;
            }

            context.HttpContext.Items[typeof(LedgerAccess)] = access;
            await next();
        }
    }
}
