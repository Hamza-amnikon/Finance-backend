using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.Repositories;
using SparkFinance.Http;
using SparkFinance.Database;

namespace AuthService.API
{
    public static class AuthApplication
    {
        public static WebApplication Build(WebApplicationBuilder builder)
        {
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(RoleService.API.Controllers.AuthController).Assembly);
            builder.Services.AddOpenApi();

            var useLoopbackProxy = builder.Configuration.GetValue<bool>("ReverseProxy:TrustLoopback");
            if (useLoopbackProxy)
            {
                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.ForwardLimit = 1;
                });
            }

            // Local Admin credentials continue to be handled only by RoleService.
            builder.Services.AddAuthentication()
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = "Finance.AuthService.Internal";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;
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

            var mySqlConnection = builder.Configuration.GetConnectionString("MySqlConnection");
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(mySqlConnection))
            {
                throw new InvalidOperationException("Configure a finance database connection string for MERIT AuthService.");
            }

            var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
            builder.Services.AddDbContext<RoleDbContext>(options =>
            {
                if (!string.IsNullOrWhiteSpace(mySqlConnection))
                {
                    options.UseMeritMySql(builder.Configuration);
                    return;
                }

                switch (databaseProvider)
                {
                    case "SqlServer":
                        options.UseSqlServer(connectionString!);
                        break;
                    case "MySql":
                        options.UseMySQL(connectionString!);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
                }
            });
            builder.Services.TryAddScoped<IUserRepository, UserRepository>();
            builder.Services.TryAddScoped<IRoleRepository, RoleRepository>();
            builder.Services.TryAddScoped<IUserPagePermissionRepository, UserPagePermissionRepository>();
            builder.Services.TryAddScoped<RoleService.API.Services.IUserPagePermissionService,
                RoleService.API.Services.UserPagePermissionService>();
            builder.Services.AddFinanceMicrosoftAuthentication(builder.Configuration, builder.Environment);

            var app = builder.Build();

            if (useLoopbackProxy)
            {
                app.UseForwardedHeaders();
            }

            app.UseFinanceRequestCancellation();
            app.UseMiddleware<DatabaseAvailabilityMiddleware>();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwaggerUI(options =>
                    options.SwaggerEndpoint("../openapi/v1.json", "MERIT Authentication Service v1"));
            }

            app.MapGet("/health", () => Results.Ok(new
            {
                service = "MERIT AuthService",
                status = "Healthy"
            }));

            return app;
        }
    }
}
