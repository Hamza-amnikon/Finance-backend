using SparkFinance.Http;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using RoleService.API;
using RoleService.API.Authentication;
using RoleService.API.Repositories;
using RoleService.API.Services;
using RoleServiceImplementation = RoleService.API.Services.RoleService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "Finance.DevelopmentAdmin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("development-login", limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
    });
}

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<RoleDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsHistoryTable(
            "__RoleMigrationsHistory",
            "security");
    });
});

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleServiceImplementation>();

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

builder.Services.AddScoped<
    IUserService,
    UserService>();

builder.Services.AddScoped<
    IUserPagePermissionService,
    UserPagePermissionService>();

builder.Services.AddScoped<
    IUserPagePermissionRepository,
    UserPagePermissionRepository>();

builder.Services.AddScoped<UsersPermissionFilter>();

builder.Services.AddFinanceMicrosoftAuthentication(
    builder.Configuration,
    builder.Environment);

var app = builder.Build();

var requiredMicrosoftScopes = builder.Configuration
    .GetSection("AzureAd:RequiredScopes")
    .Get<string[]>();

if (requiredMicrosoftScopes == null || requiredMicrosoftScopes.Length == 0)
{
    app.Logger.LogWarning(
        "Microsoft JWT access is blocked until AzureAd:RequiredScopes contains the API scope configured in Entra. " +
        "The development Admin cookie login remains available.");
}

app.UseFinanceRequestCancellation();
app.UseMiddleware<DatabaseAvailabilityMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
    app.MapDevelopmentAdmin();
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "../openapi/v1.json",
            "Finance Role Service v1");
    });

}

app.MapControllers();

app.Run();
