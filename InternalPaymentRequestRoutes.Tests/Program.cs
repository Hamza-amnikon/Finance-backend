using System.Net;
using BankingService.API.Controllers;
using BankingService.API.Data;
using BankingService.API.Repositories;
using BankingService.API.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Services:AccountingService:BaseUrl"] = "https://scope.example.test/"
});
builder.Services.AddControllers().AddApplicationPart(typeof(InternalPaymentRequestsController).Assembly);
builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseSqlServer("Server=unused;Database=unused;Integrated Security=true"));
builder.Services.AddScoped<InternalPaymentRequestRepository>();
builder.Services.AddScoped<InternalPaymentRequestService>();
var handler = new DeniedScopeHandler();
builder.Services.AddHttpClient<InternalPaymentRequestAccess>()
    .ConfigurePrimaryHttpMessageHandler(() => handler);
await using var app = builder.Build();
app.MapControllers();
await app.StartAsync();
try
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    foreach (var operation in new[] { "submit", "approve", "reject", "cancel" })
    {
        using var response = await client.PostAsJsonAsync("api/internal-payment-requests/1/" + operation, new
        {
            rowVersion = Convert.ToBase64String(new byte[8]),
            note = "Route verification"
        });
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            throw new Exception(operation + " did not reach authorization: " + response.StatusCode);
        }
        Console.WriteLine("PASS: " + operation + " resolves to the controller and rejects an unverified session.");
    }
    if (handler.Calls != 4)
    {
        throw new Exception("Status routes bypassed permission verification.");
    }
    Console.WriteLine("Request action HTTP route checks passed. No database connections were opened.");
}
finally
{
    await app.StopAsync();
}

sealed class DeniedScopeHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }
}
