using System.Text.Json.Serialization;
using AccountingService.API.Data;
using AccountingService.API.Repositories;
using AccountingService.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
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
    options.UseSqlServer(
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

var app = builder.Build();

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

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException)
        when (
            context.RequestAborted
                .IsCancellationRequested)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 499;
        }
    }
});

app.MapControllers();

app.Run();
