using AccountingService.API.Controllers;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

await Check(new LedgerAccess(401, false, []), "Get", "?companyId=2", 401);
await Check(new LedgerAccess(200, false, [2]), "Get", "?companyId=1", 403);
await Check(new LedgerAccess(200, false, [2]), "Accounts", "?companyId=1", 403);
await Check(new LedgerAccess(200, false, [2]), "Get", "?companyId=2", 200);
await Check(new LedgerAccess(200, false, []), "Get", "?companyId=2", 403);
await Check(new LedgerAccess(200, false, [2]), "Get", "", 403);
await Check(new LedgerAccess(200, false, [2]), "Get", "?companyId=2&companyId=1", 403);
await Check(new LedgerAccess(200, false, [2]), "Companies", "", 200);
await Check(new LedgerAccess(200, true, []), "Get", "?companyId=1", 200);
await Check(null, "Get", "?companyId=2", 503);
Console.WriteLine("All General Ledger authorization filter checks passed.");

static async Task Check(LedgerAccess? access, string action, string query, int expected)
{
    var http = new DefaultHttpContext();
    http.Request.QueryString = new QueryString(query);
    var descriptor = new ActionDescriptor
    {
        RouteValues = new Dictionary<string, string?> { ["action"] = action }
    };
    var actionContext = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
    var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    var filter = new GeneralLedgerAccessFilter(new FakeAccess(access), NullLogger<GeneralLedgerAccessFilter>.Instance);
    var called = false;
    await filter.OnActionExecutionAsync(context, () =>
    {
        called = true;
        return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
    });
    var status = called ? 200 : (context.Result as ObjectResult)?.StatusCode;
    if (status != expected || (called && !Equals(http.Items[typeof(LedgerAccess)], access)))
    {
        throw new Exception($"Failed {action} {query}: expected {expected}, got {status}.");
    }
    Console.WriteLine($"PASS {action} {query}: {expected}");
}

sealed class FakeAccess(LedgerAccess? access) : IGeneralLedgerAccess
{
    public Task<LedgerAccess> GetAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        return access == null
            ? Task.FromException<LedgerAccess>(new HttpRequestException("Verification unavailable"))
            : Task.FromResult(access);
    }
}
