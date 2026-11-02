using System.Text.Json.Serialization;
using AccountingService.API.Data;
using AccountingService.API.Repositories;
using AccountingService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<AccountingDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.MigrationsHistoryTable(
                "__AccountingMigrationsHistory",
                "accounting");
        });
});

builder.Services.AddScoped<
    IAccountRepository,
    AccountRepository>();

builder.Services.AddScoped<
    IAccountService,
    AccountService>();

builder.Services.AddScoped<
    IChartOfAccountsSetupService,
    ChartOfAccountsSetupService>();

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
            "Accounting Service API");

        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("ReactPolicy");

app.MapControllers();

app.Run();