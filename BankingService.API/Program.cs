using BankingService.API.Data;
using BankingService.API.Repositories;
using BankingService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<BankingExchangeRateOptions>()
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
    IBankingExchangeRateService,
    BankingExchangeRateService>((services, client) =>
    {
        var options = services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<BankingExchangeRateOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__BankingMigrationsHistory",
                "banking")));

builder.Services.AddScoped<
    IBankAccountRepository,
    BankAccountRepository>();

builder.Services.AddScoped<
    IBankAccountService,
    BankAccountService>();

builder.Services.AddScoped<
    IBankTransactionRepository,
    BankTransactionRepository>();

builder.Services.AddScoped<
    IBankTransactionService,
    BankTransactionService>();

builder.Services.AddScoped<
    IBankTransferRepository,
    BankTransferRepository>();

builder.Services.AddScoped<
    IBankTransferService,
    BankTransferService>();

builder.Services.AddScoped<
    IBankReconciliationRepository,
    BankReconciliationRepository>();

builder.Services.AddScoped<
    IBankReconciliationService,
    BankReconciliationService>();

builder.Services.Configure<PaymentBankingOptions>(builder.Configuration.GetSection("PaymentBanking"));
builder.Services.AddScoped<PaymentBankingService>();
builder.Services.AddHostedService<PaymentBankingWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "BankingService.API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();