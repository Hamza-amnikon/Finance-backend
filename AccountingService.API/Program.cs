using SparkFinance.Database;
using SparkFinance.Http;
using SparkFinance.Authentication;
using AccountingService.API.Authentication;
using System.Text.Json.Serialization;
using AccountingService.API.Data;
using AccountingService.API.Repositories;
using AccountingService.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication()
    .AddMeritJwtValidation(
        builder.Configuration.GetSection("JwtValidation"),
        AccountingAuthenticationSchemes.MeritJwt);
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<GeneralLedgerAccess>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
});
builder.Services.AddScoped<IGeneralLedgerAccess>(services => services.GetRequiredService<GeneralLedgerAccess>());
builder.Services.AddScoped<IAccountingPageAccess>(services => services.GetRequiredService<GeneralLedgerAccess>());
builder.Services.AddScoped<AccountingService.API.Controllers.GeneralLedgerAccessFilter>();
builder.Services.AddScoped<AccountingService.API.Controllers.AccountsAccessFilter>();
builder.Services.AddScoped<AccountingService.API.Controllers.TaxSettingsAccessFilter>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Finance.Accounting.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddScoped<IReconciliationReportService, ReconciliationReportService>();

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
    options.UseFinanceDatabase(
        builder.Configuration,
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

builder.Services.AddScoped<GeneralVatSetupService>();

builder.Services.AddScoped<
    IJournalEntryRepository,
    JournalEntryRepository>();

builder.Services.AddScoped<
    IJournalEntryService,
    JournalEntryService>();

builder.Services.AddScoped<
    IGeneralLedgerRepository,
    GeneralLedgerRepository>();

builder.Services.AddScoped<
    IGeneralLedgerService,
    GeneralLedgerService>();

builder.Services.AddScoped<
    ITrialBalanceRepository,
    TrialBalanceRepository>();

builder.Services.AddScoped<
    ITrialBalanceService,
    TrialBalanceService>();

builder.Services.AddScoped<
    IFinancialReportService,
    FinancialReportService>();

builder.Services.AddScoped<
    ITaxReportService,
    TaxReportService>();

builder.Services.AddScoped<
    ISalesInvoicePostingService,
    SalesInvoicePostingService>();

builder.Services.AddScoped<
    ISalesCreditNotePostingService,
    SalesCreditNotePostingService>();

builder.Services.AddScoped<
    IPurchaseBillPostingService,
    PurchaseBillPostingService>();

builder.Services.AddScoped<
    IPaymentReceivedPostingService,
    PaymentReceivedPostingService>();

builder.Services.AddScoped<
    IPaymentMadePostingService,
    PaymentMadePostingService>();

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

builder.Services.AddScoped<PurchaseBillReversalService>();

var app = builder.Build();

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownProxies.Add(IPAddress.Loopback);
app.UseForwardedHeaders(forwardedHeaders);

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

app.UseFinanceRequestCancellation();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.RunWithDevelopmentInstanceGuard();
