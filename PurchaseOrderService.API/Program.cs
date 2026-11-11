using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.Repositories;
using PurchaseOrderService.API.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<PurchaseOrderDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__PurchaseOrderMigrationsHistory",
                "purchases")));

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
