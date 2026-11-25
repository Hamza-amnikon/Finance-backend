using SparkFinance.Database;
using Microsoft.EntityFrameworkCore;
using PermissionService.API.Authentication;
using PermissionService.API.CallOtherApis;
using PermissionService.API.Data;
using PermissionService.API.Repositories;
using PermissionService.API.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PermissionService.API.Authentication.RolePermissionsAccessFilter>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

#if DEBUG
if (string.IsNullOrWhiteSpace(connectionString))
{
    var localConfiguration = new ConfigurationBuilder()
        .AddUserSecrets<PermissionDbContext>(optional: true)
        .Build();

    connectionString =
        localConfiguration.GetConnectionString("DefaultConnection");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    var localSettingsPath = Path.Combine(
        AppContext.BaseDirectory,
        "appsettings.Local.json");

    var localConfiguration = new ConfigurationBuilder()
        .AddJsonFile(
            localSettingsPath,
            optional: true)
        .Build();

    connectionString =
        localConfiguration.GetConnectionString("DefaultConnection");
}
#endif

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<PermissionDbContext>(options =>
{
    if (builder.Configuration["Database:Provider"] == "MySql")
    {
        options.UseMySQL(connectionString);
    }
    else
    {
        options.UseFinanceDatabase(builder.Configuration, connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsHistoryTable("__PermissionMigrationsHistory", "security");
        });
    }
});

builder.Services.AddScoped<
    IPermissionRepository,
    PermissionRepository>();

builder.Services.AddScoped<
    IPermissionService,
    PermissionService.API.Services.PermissionService>();

builder.Services.AddScoped<
    IRolePermissionRepository,
    RolePermissionRepository>();

builder.Services.AddScoped<
    IRolePermissionService,
    RolePermissionService>();

var roleServiceSection =
    builder.Configuration.GetSection("Services:RoleService");

var roleServiceBaseUrl =
    roleServiceSection["BaseUrl"];

if (!Uri.TryCreate(
    roleServiceBaseUrl,
    UriKind.Absolute,
    out var roleServiceUri) ||
    (roleServiceUri.Scheme != Uri.UriSchemeHttps &&
     roleServiceUri.Scheme != Uri.UriSchemeHttp))
{
    throw new InvalidOperationException(
        "Configure a valid Services:RoleService:BaseUrl.");
}

var roleServiceTimeoutSeconds =
    roleServiceSection.GetValue<int?>("TimeoutSeconds");

if (!roleServiceTimeoutSeconds.HasValue ||
    roleServiceTimeoutSeconds.Value <= 0 ||
    roleServiceTimeoutSeconds.Value > 300)
{
    throw new InvalidOperationException(
        "Configure Services:RoleService:TimeoutSeconds between 1 and 300.");
}

builder.Services.AddHttpClient<
    IRoleServiceClient,
    RoleServiceClient>(client =>
    {
        client.BaseAddress = new Uri(
            roleServiceUri.AbsoluteUri.TrimEnd('/') + "/");

        client.Timeout = TimeSpan.FromSeconds(
            roleServiceTimeoutSeconds.Value);
    });

var companyServiceSection =
    builder.Configuration.GetSection("Services:CompanyService");

var companyServiceBaseUrl =
    companyServiceSection["BaseUrl"];

if (!Uri.TryCreate(
    companyServiceBaseUrl,
    UriKind.Absolute,
    out var companyServiceUri) ||
    companyServiceUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        "Configure a valid HTTPS Services:CompanyService:BaseUrl.");
}

var companyServiceTimeoutSeconds =
    companyServiceSection.GetValue<int?>("TimeoutSeconds");

if (!companyServiceTimeoutSeconds.HasValue ||
    companyServiceTimeoutSeconds.Value <= 0 ||
    companyServiceTimeoutSeconds.Value > 300)
{
    throw new InvalidOperationException(
        "Configure Services:CompanyService:TimeoutSeconds between 1 and 300.");
}

builder.Services.AddHttpClient<
    ICompanyServiceClient,
    CompanyServiceClient>(client =>
    {
        client.BaseAddress = new Uri(
            companyServiceUri.AbsoluteUri.TrimEnd('/') + "/");

        client.Timeout = TimeSpan.FromSeconds(
            companyServiceTimeoutSeconds.Value);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    });

builder.Services.AddFinanceLoginSession(
    builder.Configuration,
    builder.Environment,
    roleServiceUri,
    roleServiceTimeoutSeconds.Value);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "../openapi/v1.json",
            "Finance Permission Service v1");
    });
}

app.UseHttpsRedirection();

app.UseMiddleware<PermissionRequestFailureMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
