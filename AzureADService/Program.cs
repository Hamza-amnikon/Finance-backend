using AzureADService.Configuration;
using AzureADService.Services;
using PermissionService.API.Authentication;

var builder = WebApplication.CreateBuilder(args);
var roleService = builder.Configuration.GetSection("Services:RoleService");
var timeout = roleService.GetValue<int>("TimeoutSeconds");

if (!Uri.TryCreate(roleService["BaseUrl"], UriKind.Absolute, out var roleServiceUri) ||
    roleServiceUri.Scheme != Uri.UriSchemeHttps ||
    timeout is < 1 or > 300)
{
    throw new InvalidOperationException("Configure the HTTPS RoleService URL and request timeout.");
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
