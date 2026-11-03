using BillService.API.Data;
using BillService.API.Repositories;
using BillService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers and API documentation
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Database
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<BillDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.MigrationsHistoryTable(
            "__BillMigrationsHistory",
            "purchases"));
});

// Bill repository
builder.Services.AddScoped<
    IBillRepository,
    BillRepository>();

// Bill services
builder.Services.AddScoped<
    IBillService,
    BillService.API.Services.BillService>();

builder.Services.AddScoped<
    IBillCalculator,
    BillCalculator>();

builder.Services.AddScoped<
    IBillConversionService,
    BillConversionService>();

builder.Services.AddHttpClient<
    IBillAccountingApiService,
    BillAccountingApiService>(httpClient =>
    {
        httpClient.BaseAddress = new Uri(
            builder.Configuration["Services:AccountingService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "AccountingService BaseUrl is missing from appsettings.json."));
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    });

builder.Services.AddHttpClient<
    IBillExchangeRateService,
    BillExchangeRateService>(httpClient =>
    {
        httpClient.BaseAddress = new Uri(
            builder.Configuration["Services:ExchangeRateService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "ExchangeRateService BaseUrl is missing from appsettings.json."));
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    });

// React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Bill Service API");
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("ReactPolicy");

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
