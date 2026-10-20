using Microsoft.EntityFrameworkCore;
using PaymentMadeService.API.Data;
using PaymentMadeService.API.Repositories;
using PaymentMadeService.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from appsettings.json.");

builder.Services.AddDbContext<PaymentMadeDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.MigrationsHistoryTable(
            "__PaymentMadeMigrationsHistory",
            "purchases")));

builder.Services.AddScoped<IPaymentMadeRepository, PaymentMadeRepository>();

builder.Services.AddScoped<
    IPaymentMadeService,
    PaymentMadeService.API.Services.PaymentMadeService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Payments Made API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
