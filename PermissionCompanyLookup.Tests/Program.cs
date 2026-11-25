using System.Net;
using Microsoft.AspNetCore.Http;
using PermissionService.API.CallOtherApis;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("FAIL " + message);
    }
    checks++;
    Console.WriteLine("PASS " + message);
}

var incoming = new DefaultHttpContext();
incoming.Request.Headers.Cookie = "Finance.Microsoft=test-session";
incoming.Request.Headers.Authorization = "Bearer test-token";
incoming.Request.Headers["X-Finance-Authentication"] = "microsoft";
incoming.Request.Headers["X-Claim-Role"] = "Admin";
var handler = new CompanyHandler(HttpStatusCode.OK, "[{\"companyId\":7,\"companyName\":\"Company A\"}]");
using var client = new HttpClient(handler) { BaseAddress = new Uri("https://company.invalid/") };
var service = new CompanyServiceClient(client, new HttpContextAccessor { HttpContext = incoming });
var company = await service.GetByIdAsync(7, CancellationToken.None);
Check(company?.CompanyId == 7, "The selected authorized company loads");
Check(handler.Path == "/api/expenses/lookups/company-setup/role-permissions", "Company validation uses the Role Permissions lookup");
Check(handler.Cookie == "Finance.Microsoft=test-session" && handler.Authorization == "Bearer test-token" &&
    handler.Method == "microsoft", "The caller session is forwarded to the configured company service");
Check(!handler.ForgedRole, "Untrusted role headers are not forwarded");
Check(await service.GetByIdAsync(8, CancellationToken.None) == null, "A company outside the returned scope is not accepted");

foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable })
{
    using var failingClient = new HttpClient(new CompanyHandler(status, "[]"))
    {
        BaseAddress = new Uri("https://company.invalid/")
    };
    var denied = new CompanyServiceClient(failingClient, new HttpContextAccessor { HttpContext = incoming });
    try
    {
        await denied.GetByIdAsync(7, CancellationToken.None);
        throw new Exception("FAIL: dependency rejection was ignored");
    }
    catch (HttpRequestException exception)
    {
        Check(exception.StatusCode == status, "Dependency status " + (int)status + " cannot grant access");
    }
}
Console.WriteLine($"{checks} checks passed without database writes.");

sealed class CompanyHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public string? Path { get; private set; }
    public string? Cookie { get; private set; }
    public string? Authorization { get; private set; }
    public string? Method { get; private set; }
    public bool ForgedRole { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Path = request.RequestUri?.AbsolutePath;
        Cookie = request.Headers.TryGetValues("Cookie", out var cookies) ? cookies.Single() : null;
        Authorization = request.Headers.Authorization?.ToString();
        Method = request.Headers.TryGetValues("X-Finance-Authentication", out var methods) ? methods.Single() : null;
        ForgedRole = request.Headers.Contains("X-Claim-Role");
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
