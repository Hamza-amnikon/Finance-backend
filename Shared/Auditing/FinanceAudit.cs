using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SparkFinance.Auditing
{
    public static class FinanceAudit
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
        // Financial facts only. Never capture file bytes, passwords, tokens, contact/address or bank-account details.
        public static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id",
            "CompanyId",
            "AccountingCompanyId",
            "Branch",
            "Name",
            "Code",
            "Type",
            "Status",
            "IsActive",
            "Currency",
            "ReportingCurrency",
            "Country",
            "CountryCode",
            "TaxTreatment",
            "Date",
            "InvoiceDate",
            "DueDate",
            "BillDate",
            "CreditDate",
            "CreditNoteDate",
            "PaymentDate",
            "TransactionDate",
            "TransferDate",
            "StatementDate",
            "EntryDate",
            "ServicePeriodStart",
            "ServicePeriodEnd",
            "Reference",
            "InvoiceNumber",
            "BillNumber",
            "CreditNoteNumber",
            "VendorCreditNumber",
            "PaymentNumber",
            "EntryNumber",
            "EstimateNumber",
            "PurchaseOrderNumber",
            "ClientId",
            "ClientName",
            "VendorId",
            "VendorName",
            "Supplier",
            "Employee",
            "Category",
            "Amount",
            "SubTotal",
            "DiscountTotal",
            "TaxTotal",
            "GrandTotal",
            "TotalDebit",
            "TotalCredit",
            "Rate",
            "RateDate",
            "RateToInr",
            "ExchangeRate",
            "RateBaseCurrency",
            "RateQuoteCurrency",
            "BankAmount",
            "BankChargeAmount",
            "BankCurrency",
            "SettlementType",
            "BankAccountId",
            "SourceAccountId",
            "DestinationAccountId",
            "FromBankAccountId",
            "ToBankAccountId",
            "Direction",
            "SalesInvoiceId",
            "SalesCreditNoteId",
            "SalesInvoiceItemId",
            "BillId",
            "VendorCreditId",
            "TargetInvoiceId",
            "SourceInvoiceId",
            "ExpenseId",
            "ExpenseClaimId",
            "JournalEntryId",
            "AccountId",
            "SourceDocumentId",
            "SourceDocumentNumber",
            "Source",
            "ExpenseAccountId",
            "PayableAccountId",
            "PaymentAccountId",
            "DebitAccountId",
            "CreditAccountId",
            "CreatedBy",
            "ApprovedBy",
            "PaidBy",
            "ApprovedDate",
            "PaidDate",
            "PaymentReference",
            "CreatedDate",
            "ModifiedDate",
            "IssuedDate",
            "PostedDate",
            "VoidedDate",
            "VoidReason",
            "CancelledDate",
            "ReversedDate",
            "ReversalOfJournalEntryId",
            "ReversalOfTransactionId",
            "LineNumber",
            "ServiceName",
            "Quantity",
            "Unit",
            "UnitPrice",
            "DiscountAmount",
            "DiscountType",
            "DiscountValue",
            "TaxSettingId",
            "TaxSettingSnapshotJson",
            "UsesTaxSettings",
            "AccountingReversalPending",
            "TaxTypeId",
            "Rate",
            "EffectiveFrom",
            "EffectiveTo",
            "ComponentsJson",
            "TaxRate",
            "TaxAmount",
            "LineTotal",
            "LineSubTotal",
            "DebitAmount",
            "CreditAmount",
            "BaseDebitAmount",
            "BaseCreditAmount",
            "FileName",
            "ContentType",
            "Size",
            "OpeningBalance",
            "OpeningDate",
            "StatementBalance",
            "BookBalance",
            "Difference",
            "IsReconciled",
            "CreditAmount",
            "ApplicationDate"
        };
        public static Dictionary<string, object?> Snapshot(PropertyValues values) => values.Properties.Where(property => Fields.Contains(property.Name)).ToDictionary(property => property.Name, property => values[property]);
        public static async Task WriteAsync(
            DbContext context,
            string entityType,
            string entityId,
            string action,
            Dictionary<string, object?>? before,
            Dictionary<string, object?>? after,
            CancellationToken cancellationToken
        )
        {
            if (!context.Database.IsSqlServer())
            {
                return;
            }

            var http = new HttpContextAccessor().HttpContext;
            var identity = http?.User;
            var authenticated = identity?.Identity?.IsAuthenticated == true;
            var actor = authenticated ? identity!.FindFirstValue(ClaimTypes.NameIdentifier) ?? identity!.FindFirstValue("sub") : null;
            var kind = actor != null ? "Authenticated" : http == null ? "System" : "Unattributed";
            if (actor == null && http?.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true)
            {
                var declared = http.Request.Headers["X-Claim-Operator"].ToString().Trim();
                if (!string.IsNullOrWhiteSpace(declared))
                {
                    actor = declared;
                    kind = "Local operator (unverified)";
                }
            }

            actor ??= kind == "System" ? "System / background process" : "Identity unavailable";
            actor = actor[..Math.Min(actor.Length, 200)];
            var snapshot = after ?? before;
            var branch = snapshot?.GetValueOrDefault("Branch")?.ToString();
            int? companyId = int.TryParse(snapshot?.GetValueOrDefault("CompanyId")?.ToString(), out var company) ? company : null;
            // Accounting company IDs can resolve a branch only when the mapping is unique.
            // Never infer a branch from currency or from an unrelated service's company ID.
            if (string.IsNullOrWhiteSpace(branch) && companyId.HasValue &&
                context.GetType().Assembly.GetName().Name == "AccountingService.API")
            {
                var mappedBranches = await context.Database.SqlQueryRaw<string>(
                    """
                    IF COL_LENGTH(N'sales.BranchMasters', N'AccountingCompanyId') IS NOT NULL
                        EXEC sp_executesql
                            N'SELECT DISTINCT Name AS Value FROM sales.BranchMasters WHERE AccountingCompanyId = @CompanyId',
                            N'@CompanyId int', @CompanyId = {0};
                    ELSE
                        SELECT CAST(NULL AS nvarchar(100)) AS Value WHERE 1 = 0;
                    """, companyId.Value
                ).ToListAsync(cancellationToken);

                if (mappedBranches.Count == 1)
                {
                    branch = mappedBranches[0];
                }
            }
            var service = context.GetType().Assembly.GetName().Name ?? "Unknown service";
            var beforeJson = before == null ? null : JsonSerializer.Serialize(before, JsonOptions);
            var afterJson = after == null ? null : JsonSerializer.Serialize(after, JsonOptions);
            var correlation = http?.TraceIdentifier;
            await context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                IF OBJECT_ID(N'audit.Events', N'U') IS NOT NULL
                INSERT INTO audit.Events (AtUtc, Service, EntityType, EntityId, Action, Actor, ActorKind,
                    Branch, CompanyId, CorrelationId, BeforeJson, AfterJson)
                VALUES ({DateTimeOffset.UtcNow}, {service}, {entityType}, {entityId}, {action}, {actor}, {kind},
                    {branch}, {companyId}, {correlation}, {beforeJson}, {afterJson});",
                cancellationToken
            );
        }
    }

    public abstract class AuditedDbContext : DbContext
    {
        protected AuditedDbContext(DbContextOptions options) : base(options)
        {
        }

        public override int SaveChanges() => SaveChanges(true);
        public override int SaveChanges(bool acceptAllChangesOnSuccess) => SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
        public override async Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default
        )
        {
            ChangeTracker.DetectChanges();
            var changes = ChangeTracker.Entries().Where(entry => entry.Metadata.GetSchema() != "audit" && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(entry => new { Entry = entry, State = entry.State, ExcludedChanges = entry.Properties.Where(property => property.IsModified && !property.Metadata.IsConcurrencyToken && !FinanceAudit.Fields.Contains(property.Metadata.Name)).Select(property => property.Metadata.Name).Order().ToArray(), Before = entry.State == EntityState.Added ? null : FinanceAudit.Snapshot(entry.OriginalValues) }).ToList();
            if (!changes.Any() || !Database.IsSqlServer())
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            var ownTransaction = Database.CurrentTransaction == null;
            await using var transaction = ownTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                foreach (var change in changes)
                {
                    var after = change.State == EntityState.Deleted ? null : FinanceAudit.Snapshot(change.Entry.CurrentValues);
                    if (change.State == EntityState.Modified && change.ExcludedChanges.Length > 0)
                    {
                        after!["Other fields changed (values excluded)"] = string.Join(", ", change.ExcludedChanges);
                    }

                    if (change.State == EntityState.Modified && JsonSerializer.Serialize(change.Before) == JsonSerializer.Serialize(after))
                    {
                        continue;
                    }

                    var key = change.Entry.Metadata.FindPrimaryKey();
                    if (key == null)
                    {
                        continue;
                    }

                    var id = string.Join(
                                            "/",
                                            key.Properties.Select(property => change.Entry.Property(property.Name).CurrentValue?.ToString())
                                        );
                    var action = change.State == EntityState.Added ? "Created" : change.State == EntityState.Deleted ? "Deleted" : "Updated";
                    if (change.Before?.GetValueOrDefault("Status")?.ToString() != after?.GetValueOrDefault("Status")?.ToString() && change.State == EntityState.Modified)
                    {
                        action = "Status changed";
                    }

                    await FinanceAudit.WriteAsync(this, change.Entry.Metadata.ClrType.Name, id, action, change.Before, after, cancellationToken);
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }
        }
    }
}
