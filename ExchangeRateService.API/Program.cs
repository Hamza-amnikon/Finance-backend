using ExchangeRateService.API.Data;
using ExchangeRateService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<ExchangeRateProviderOptions>()
    .Bind(builder.Configuration.GetSection("ExchangeRates"))
    .ValidateDataAnnotations()
    .Validate(value => Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https",
        "The provider must use HTTPS.").ValidateOnStart();
builder.Services.AddOptions<DailyRateOptions>()
    .Bind(builder.Configuration.GetSection("DailyRateAgent"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHttpClient<IDailyRateProvider, ExchangeRateProvider>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExchangeRateProviderOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddDbContext<ExchangeRateDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is missing.")));
builder.Services.AddScoped<IExchangeRateService, StoredExchangeRateService>();
builder.Services.AddScoped<DailyRateAgent>();
builder.Services.AddHostedService<DailyRateWorker>();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Exchange Rates API"));
}
else app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException)
        when (context.RequestAborted.IsCancellationRequested)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 499;
        }
    }
});

app.MapControllers();
app.Run();
