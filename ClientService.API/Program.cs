using ClientService.API.Data;
using ClientService.API.Repositories;
using ClientService.API.Services;
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

builder.Services.AddDbContext<ClientDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Client repository
builder.Services.AddScoped<
    IClientRepository,
    ClientRepository>();

// Client service
builder.Services.AddScoped<
    IClientService,
    ClientService.API.Services.ClientService>();

// Branch lookup API
builder.Services.AddHttpClient<
    IBranchLookupService,
    BranchLookupService>(httpClient =>
    {
        var baseUrl = builder.Configuration[
            "Services:BranchService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "BranchService BaseUrl is missing from appsettings.json.");

        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    });

// Country Master repository
builder.Services.AddScoped<
    ICountryMasterRepository,
    CountryMasterRepository>();

// Country Master service
builder.Services.AddScoped<
    ICountryMasterService,
    CountryMasterService>();

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
            "Client Service API");
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("ReactPolicy");

app.MapControllers();

app.Run();