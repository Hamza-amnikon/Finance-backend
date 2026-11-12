using SparkFinance.Http;
using DashboardService.API.Data;
using DashboardService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<DashboardExchangeRateOptions>()
    .Bind(builder.Configuration.GetSection("ExchangeRates"))
    .ValidateDataAnnotations()
    .Validate(
        options =>
            Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment),
        "Configure a valid HTTPS exchange-rate provider base URL.")
    .ValidateOnStart();

builder.Services.AddHttpClient<
    IDashboardExchangeRateService,
    DashboardExchangeRateService>((services, client) =>
    {
        var options = services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<DashboardExchangeRateOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: new[] { -1983577846 })));

builder.Services.AddHostedService(serviceProvider =>
    new LocalDbKeepAliveWorker(
        connectionString,
        serviceProvider.GetRequiredService<ILogger<LocalDbKeepAliveWorker>>()));

builder.Services.AddScoped<
    IDashboardService,
    global::DashboardService.API.Services.DashboardService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "DashboardService.API v1");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.UseFinanceRequestCancellation();

app.MapControllers();

app.Run();
