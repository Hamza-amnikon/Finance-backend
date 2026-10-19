using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.Data;
using SalesEstimateService.API.Services;
using SalesEstimateService.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<SalesEstimateDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "estimates")));

builder.Services.AddScoped<
    ISalesEstimateCalculator,
    SalesEstimateCalculator>();

builder.Services.AddScoped<
    ISalesEstimateRepository,
    SalesEstimateRepository>();

builder.Services.AddScoped<
    ISalesEstimateService,
    global::SalesEstimateService.API.Services.SalesEstimateService>();

builder.Services.AddHttpClient<
    IClientApiService,
    ClientApiService>(httpClient =>
    {
        var baseUrl = builder.Configuration["Services:ClientService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "ClientService BaseUrl is missing from appsettings.json.");

        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    });

builder.Services.AddHttpClient<
    ICurrencyApiService,
    CurrencyApiService>(httpClient =>
    {
        var baseUrl = builder.Configuration["Services:SalesInvoiceService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "SalesInvoiceService BaseUrl is missing from appsettings.json.");

        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    });

builder.Services.AddScoped<
    ISalesEstimateConversionService,
    SalesEstimateConversionService>();

builder.Services.AddHttpClient<IInvoiceApiService, InvoiceApiService>(httpClient =>
{
    var baseUrl = builder.Configuration["Services:SalesInvoiceService:BaseUrl"]
        ?? throw new InvalidOperationException("SalesInvoiceService BaseUrl is missing from appsettings.json.");

    httpClient.BaseAddress = new Uri(baseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Sales Estimate API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
