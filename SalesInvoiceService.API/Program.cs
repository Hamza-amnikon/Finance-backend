using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.Repositories;
using SalesInvoiceService.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<SalesInvoiceDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "sales")));

builder.Services.AddScoped<
    ISalesInvoiceCalculator,
    SalesInvoiceCalculator>();

builder.Services.AddScoped<
    ICurrencyRepository,
    CurrencyRepository>();

builder.Services.AddScoped<
    ICurrencyService,
    CurrencyService>();

builder.Services.AddScoped<
    IPaymentModeRepository,
    PaymentModeRepository>();

builder.Services.AddScoped<
    IPaymentModeService,
    PaymentModeService>();

builder.Services.AddScoped<
    ISalesInvoiceRepository,
    SalesInvoiceRepository>();

builder.Services.AddScoped<
    ISalesInvoiceService,
    global::SalesInvoiceService.API.Services.SalesInvoiceService>();

builder.Services.AddHttpClient<
    IClientApiService,
    ClientApiService>(httpClient =>
    {
        var baseUrl = builder.Configuration[
            "Services:ClientService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "ClientService BaseUrl is missing from appsettings.json.");

        httpClient.BaseAddress = new Uri(baseUrl);
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Sales Invoice API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();