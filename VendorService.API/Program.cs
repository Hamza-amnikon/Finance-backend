using Microsoft.EntityFrameworkCore;
using VendorService.API.Data;
using VendorService.API.Repositories;
using VendorService.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<VendorDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
            sqlOptions.MigrationsHistoryTable(
                "__VendorMigrationsHistory",
                "purchases")));

// The location context reads the existing master table in the same database.
// It owns no tables and is never used to run migrations.
builder.Services.AddDbContext<VendorLocationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<ICountryMasterRepository, CountryMasterRepository>();
builder.Services.AddScoped<ICountryMasterService, CountryMasterService>();

builder.Services.AddScoped<IVendorRepository, VendorRepository>();

builder.Services.AddScoped<
    IVendorService,
    VendorService.API.Services.VendorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Vendor Service API v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();