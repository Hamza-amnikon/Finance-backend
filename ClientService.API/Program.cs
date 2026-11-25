using SparkFinance.Http;
using ClientService.API.Data;
using ClientService.API.Repositories;
using ClientService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<FinanceCompanyAccess>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    UseCookies = false
});

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

var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<ClientDbContext>(options =>
{
    switch (databaseProvider)
    {
        case "SqlServer":
            options.UseSqlServer(connectionString);
            break;
        case "MySql":
            options.UseMySQL(connectionString);
            break;
        default:
            throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
    }
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

app.UseFinanceRequestCancellation();

if (databaseProvider == "MySql" && builder.Configuration.GetValue<bool>("Database:AllowMySqlClientWrites"))
{
    var frontendOrigin = builder.Configuration["Services:Frontend:Origin"];
    if (!Uri.TryCreate(frontendOrigin, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps ||
        !string.IsNullOrEmpty(origin.UserInfo) || frontendOrigin != origin.GetLeftPart(UriPartial.Authority))
    {
        throw new InvalidOperationException("Configure the trusted HTTPS frontend origin before enabling MySQL client writes.");
    }

    app.Use(async (context, next) =>
    {
        var method = context.Request.Method;
        if ((HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method)) &&
            !string.Equals(context.Request.Headers.Origin, frontendOrigin, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    });
}

app.MapControllers();

app.Run();
