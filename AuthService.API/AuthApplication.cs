using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.Repositories;
using SparkFinance.Http;

namespace AuthService.API
{
    public static class AuthApplication
    {
        public static WebApplication Build(WebApplicationBuilder builder)
        {
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(RoleService.API.Controllers.AuthController).Assembly);
            builder.Services.AddOpenApi();

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

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for MERIT AuthService.");
            }

            builder.Services.AddDbContext<RoleDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.TryAddScoped<IUserRepository, UserRepository>();
            builder.Services.TryAddScoped<IRoleRepository, RoleRepository>();
            builder.Services.TryAddScoped<IUserPagePermissionRepository, UserPagePermissionRepository>();
            builder.Services.TryAddScoped<RoleService.API.Services.IUserPagePermissionService,
                RoleService.API.Services.UserPagePermissionService>();
            builder.Services.AddFinanceMicrosoftAuthentication(builder.Configuration, builder.Environment);

            var app = builder.Build();

            app.UseFinanceRequestCancellation();
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
