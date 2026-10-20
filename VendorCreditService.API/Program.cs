using Microsoft.EntityFrameworkCore;
using VendorCreditService.API.Data;
using VendorCreditService.API.Repositories;
using VendorCreditService.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<VendorCreditDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__VendorCreditMigrationsHistory",
                "purchases")));

builder.Services.AddScoped<
    IVendorCreditRepository,
    VendorCreditRepository>();

builder.Services.AddScoped<
    IVendorCreditCalculator,
    VendorCreditCalculator>();

builder.Services.AddScoped<
    IVendorCreditService,
    VendorCreditService.API.Services.VendorCreditService>();

builder.Services.AddScoped<ICreditApplicationRepository, CreditApplicationRepository>();

builder.Services.AddScoped<ICreditApplicationService, CreditApplicationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Vendor Credit Service API v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
