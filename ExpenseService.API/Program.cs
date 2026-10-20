using ExpenseService.API.Data;
using ExpenseService.API.Repositories;
using ExpenseService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<ExpenseDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__ExpenseMigrationsHistory",
                "expenses")));

builder.Services.AddScoped<
    IExpenseRepository,
    ExpenseRepository>();

builder.Services.AddScoped<
    IExpenseService,
    ExpenseService.API.Services.ExpenseService>();

builder.Services.AddScoped<
    IExpenseLookupRepository,
    ExpenseLookupRepository>();

builder.Services.AddScoped<
    IExpenseLookupService,
    ExpenseLookupService>();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<ExchangeRateOptions>()
    .Bind(builder.Configuration.GetSection("ExchangeRates"))
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        options.BaseUrl.EndsWith("/") &&
        options.TimeoutSeconds > 0 &&
        options.CacheMinutes > 0 &&
        !string.IsNullOrWhiteSpace(options.ProviderName),
        "Configure a valid HTTPS exchange-rate provider.")
    .ValidateOnStart();

builder.Services.AddHttpClient<
    IExpenseExchangeRateService,
    ExpenseExchangeRateService>((services, client) =>
    {
        var options = services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<ExchangeRateOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Expenses API v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
