using System.Net;
using System.Text.Json;
using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using ExpenseService.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SparkFinance.Http;

var checks = 0;
async Task Check(string name, int companyId, bool create, bool post, bool admin, bool unavailable, string status, int expected)
{
    var payload = JsonSerializer.Serialize(new
    {
        IsAdmin = admin,
        Branches = new[] { new { Name = "Branch A", AccountingCompanyId = 1 } },
        CompanyIds = new[] { 1 },
        CreatableCompanyIds = create ? new[] { 1 } : Array.Empty<int>(),
        PostableCompanyIds = post ? new[] { 1 } : Array.Empty<int>()
    });
    var client = new HttpClient(new ScopeHandler(payload, unavailable));
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Services:AccountingService:BaseUrl"] = "https://scope.invalid/"
    }).Build();
    var access = new FinanceCompanyAccess(client, configuration, NullLogger<FinanceCompanyAccess>.Instance);
    await using var database = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
    var http = new DefaultHttpContext();
    http.Request.Method = "POST";
    var descriptor = new ActionDescriptor();
    descriptor.RouteValues["action"] = "Create";
    var actionContext = new ActionContext(http, new RouteData(), descriptor);
    var filters = new List<IFilterMetadata>();
    var executing = new ActionExecutingContext(actionContext, filters, new Dictionary<string, object?>
    {
        ["dto"] = new ExpenseDto { CompanyId = companyId, Status = status }
    }, new object());
    var called = false;
    await new ExpenseAccessFilter(access, database).OnActionExecutionAsync(executing, () =>
    {
        called = true;
        return Task.FromResult(new ActionExecutedContext(actionContext, filters, executing.Controller));
    });
    var actual = called ? 200 : (executing.Result as StatusCodeResult)?.StatusCode;
    if (actual != expected)
    {
        throw new Exception($"FAIL {name}: expected {expected}, got {actual}");
    }
    checks++;
    Console.WriteLine("PASS " + name);
}

await Check("Create grant permits a draft", 1, true, false, false, false, "Draft", 200);
await Check("View alone cannot create", 1, false, false, false, false, "Draft", 403);
await Check("Create cannot affect another company", 2, true, true, false, false, "Draft", 403);
await Check("Recording requires Post as well as Create", 1, true, false, false, false, "Recorded", 403);
await Check("Post and Create permit recording", 1, true, true, false, false, "Recorded", 200);
await Check("Post alone cannot create", 1, false, true, false, false, "Recorded", 403);
await Check("Admin retains all company actions", 2, false, false, true, false, "Recorded", 200);
await Check("Unavailable permission service fails closed", 1, true, true, false, true, "Draft", 503);
Console.WriteLine($"{checks} action permission checks passed without database writes.");

sealed class ScopeHandler(string payload, bool unavailable) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
