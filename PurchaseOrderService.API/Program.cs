using SparkFinance.Database;
using SparkFinance.Http;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.Repositories;
using PurchaseOrderService.API.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<FinanceCompanyAccess>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<PurchaseOrderDbContext>(options =>
{
    if (builder.Configuration["Database:Provider"] == "MySql" &&
        builder.Configuration.GetValue<bool>("Database:AllowMySqlPurchaseWrites"))
    {
        options.UseMySQL(connectionString);
    }
    else
    {
        options.UseFinanceDatabase(
            builder.Configuration,
            connectionString,
            sqlOptions => sqlOptions.MigrationsHistoryTable(
                "__PurchaseOrderMigrationsHistory", "purchases"));
    }
});

builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseOrderCalculator, PurchaseOrderCalculator>();
builder.Services.AddScoped<
    IPurchaseOrderService,
    PurchaseOrderService.API.Services.PurchaseOrderService>();

builder.Services.AddScoped<PurchaseOrderTaxSettingsService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Purchase Order Service API v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseFinanceRequestCancellation();

app.MapControllers();

app.Run();
