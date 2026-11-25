using AzureADService.Configuration;
using AzureADService.Services;
using PermissionService.API.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
var roleService = builder.Configuration.GetSection("Services:RoleService");
var timeout = roleService.GetValue<int>("TimeoutSeconds");

if (!Uri.TryCreate(roleService["BaseUrl"], UriKind.Absolute, out var roleServiceUri) ||
    (roleServiceUri.Scheme != Uri.UriSchemeHttps &&
     !(builder.Configuration["Database:Provider"] == "MySql" && roleServiceUri.IsLoopback &&
       roleServiceUri.Scheme == Uri.UriSchemeHttp && string.IsNullOrEmpty(roleServiceUri.UserInfo))) ||
    timeout is < 1 or > 300)
{
    throw new InvalidOperationException("Configure a trusted RoleService URL and request timeout.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<AzureAdSettings>(builder.Configuration.GetSection("Directory"));
builder.Services.AddSingleton<GraphClientFactory>();
builder.Services.AddScoped<AzureGraphService>();
builder.Services.AddFinanceLoginSession(
    builder.Configuration,
    builder.Environment,
    roleServiceUri,
    timeout);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("DirectoryRoleService", client =>
{
    client.BaseAddress = new Uri(roleServiceUri.AbsoluteUri.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(timeout);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
});
builder.Services.AddScoped<IAuthorizationHandler, DirectoryUsersAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageDirectoryUsers", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new DirectoryUsersRequirement());
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("../openapi/v1.json", "MERIT Azure AD Service"));
}

app.Run();
