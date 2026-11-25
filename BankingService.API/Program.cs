using SparkFinance.Database;
using SparkFinance.Http;
using BankingService.API.Data;
using BankingService.API.Repositories;
using BankingService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<FinanceCompanyAccess>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });

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
            (uri.Scheme == Uri.UriSchemeHttps ||
                (builder.Configuration["Database:Provider"] == "MySql" && uri.IsLoopback &&
                    uri.Scheme == Uri.UriSchemeHttp && uri.AbsolutePath == "/api/exchange-rates/")) &&
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

builder.Services.AddHttpClient<
    IPaymentAccountingApiService,
    PaymentAccountingApiService>(httpClient =>
    {
        var baseUrl = builder.Configuration[
            "Services:AccountingService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "AccountingService BaseUrl is missing from appsettings.json.");

        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    });

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseFinanceDatabase(
        builder.Configuration,
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
if (builder.Configuration["Database:Provider"] != "MySql")
{
    builder.Services.AddHostedService<PaymentBankingWorker>();
}

builder.Services.AddScoped<InternalPaymentRequestRepository>();
builder.Services.AddScoped<InternalPaymentRequestService>();
builder.Services.AddHttpClient<InternalPaymentRequestAccess>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
});

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

app.UseFinanceRequestCancellation();

app.MapControllers();

app.RunWithDevelopmentInstanceGuard();
