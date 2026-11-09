using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Service = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_AtUtc",
                schema: "audit",
                table: "Events",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Branch_AtUtc",
                schema: "audit",
                table: "Events",
                columns: new[] { "Branch", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EntityType_EntityId",
                schema: "audit",
                table: "Events",
                columns: new[] { "EntityType", "EntityId" });
            // Existing rows are a baseline of current state, not reconstructed historical changes.
            migrationBuilder.Sql("""
                DECLARE @sources TABLE (SchemaName sysname, TableName sysname, EntityType nvarchar(100), Service nvarchar(100));
                INSERT INTO @sources VALUES
                    ('sales','SalesInvoices','SalesInvoice','SalesInvoiceService.API'),
                    ('sales','SalesInvoicePayments','SalesInvoicePayment','SalesInvoiceService.API'),
                    ('sales','SalesCreditNotes','SalesCreditNote','SalesInvoiceService.API'),
                    ('dbo','ClientMaster','Client','ClientService.API'),
                    ('purchases','VendorMaster','Vendor','VendorService.API'),
                    ('estimates','SalesEstimates','SalesEstimate','SalesEstimateService.API'),
                    ('purchases','Bills','Bill','BillService.API'),
                    ('purchases','PaymentsMade','PaymentMade','PaymentMadeService.API'),
                    ('purchases','VendorCredits','VendorCredit','VendorCreditService.API'),
                    ('purchases','PurchaseOrders','PurchaseOrder','PurchaseOrderService.API'),
                    ('expenses','Expenses','Expense','ExpenseService.API'),
                    ('expenses','ExpenseClaims','ExpenseClaim','ExpenseService.API'),
                    ('expenses','BankAccounts','BankAccount','BankingService.API'),
                    ('banking','BankTransactions','BankTransaction','BankingService.API'),
                    ('accounting','Accounts','Account','AccountingService.API'),
                    ('accounting','JournalEntries','JournalEntry','AccountingService.API');
                DECLARE @schema sysname, @table sysname, @entity nvarchar(100), @service nvarchar(100), @object int,
                    @columns nvarchar(max), @branch nvarchar(200), @company nvarchar(200), @sql nvarchar(max);
                DECLARE sources CURSOR LOCAL FAST_FORWARD FOR SELECT SchemaName, TableName, EntityType, Service FROM @sources;
                OPEN sources;
                FETCH NEXT FROM sources INTO @schema, @table, @entity, @service;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @object = OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table), 'U');
                    IF @object IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @object AND name = 'Id')
                    BEGIN
                        SELECT @columns = STRING_AGG(CONVERT(nvarchar(max), 't.' + QUOTENAME(name)), ',')
                        FROM sys.columns WHERE object_id = @object AND name IN
                            ('Id','CompanyId','Branch','Currency','Status','Name','Code','Reference','InvoiceNumber','BillNumber',
                             'PaymentNumber','EntryNumber','VendorCreditNumber','CreditNoteNumber','Employee','Category',
                             'Date','InvoiceDate','BillDate','DueDate','PaymentDate','EntryDate','Amount','GrandTotal','TaxTotal','TotalDebit','TotalCredit');
                        SET @branch = CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @object AND name = 'Branch') THEN 't.[Branch]' ELSE 'NULL' END;
                        SET @company = CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @object AND name = 'CompanyId') THEN 't.[CompanyId]' ELSE 'NULL' END;
                        SET @sql = N'INSERT INTO audit.Events (AtUtc,Service,EntityType,EntityId,Action,Actor,ActorKind,Branch,CompanyId,AfterJson)
                            SELECT SYSUTCDATETIME(), @service, @entity, CONVERT(nvarchar(200),t.Id), N''Snapshot'',
                                N''Identity unavailable'', N''Unattributed'', ' + @branch + ',' + @company + ',
                                (SELECT ' + @columns + ' FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER)
                            FROM ' + QUOTENAME(@schema) + '.' + QUOTENAME(@table) + ' t;';
                        EXEC sp_executesql @sql, N'@service nvarchar(100), @entity nvarchar(100)', @service, @entity;
                    END
                    FETCH NEXT FROM sources INTO @schema, @table, @entity, @service;
                END
                CLOSE sources;
                DEALLOCATE sources;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events",
                schema: "audit");
        }
    }
}
