using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBankReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankReconciliations",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    StatementOpeningBalance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StatementClosingBalance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliations", x => x.Id);
                    table.CheckConstraint("CK_BankReconciliation_Completion", "([Status] = 'Draft' AND [CompletedDate] IS NULL) OR ([Status] = 'Completed' AND [CompletedDate] IS NOT NULL)");
                    table.CheckConstraint("CK_BankReconciliation_Currency", "LEN([Currency]) = 3");
                    table.CheckConstraint("CK_BankReconciliation_Period", "[PeriodStart] <= [PeriodEnd]");
                    table.CheckConstraint("CK_BankReconciliation_Precision", "[CurrencyDecimalPlaces] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_BankReconciliation_Status", "[Status] IN ('Draft', 'Completed')");
                    table.ForeignKey(
                        name: "FK_BankReconciliations_BankAccounts_BankAccountId_CompanyId",
                        columns: x => new { x.BankAccountId, x.CompanyId },
                        principalSchema: "expenses",
                        principalTable: "BankAccounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankReconciliationId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MatchedBankTransactionId = table.Column<int>(type: "int", nullable: true),
                    MatchedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                    table.CheckConstraint("CK_BankStatementLine_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_BankStatementLine_Direction", "[Direction] IN (1, 2)");
                    table.CheckConstraint("CK_BankStatementLine_LineNumber", "[LineNumber] > 0");
                    table.CheckConstraint("CK_BankStatementLine_Match", "([MatchedBankTransactionId] IS NULL AND [MatchedDate] IS NULL) OR ([MatchedBankTransactionId] IS NOT NULL AND [MatchedDate] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankReconciliations_BankReconciliationId",
                        column: x => x.BankReconciliationId,
                        principalSchema: "banking",
                        principalTable: "BankReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankTransactions_MatchedBankTransactionId",
                        column: x => x.MatchedBankTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_BankAccountId_CompanyId",
                schema: "banking",
                table: "BankReconciliations",
                columns: new[] { "BankAccountId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_BankAccountId_PeriodStart_PeriodEnd",
                schema: "banking",
                table: "BankReconciliations",
                columns: new[] { "BankAccountId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_RequestId",
                schema: "banking",
                table: "BankReconciliations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankReconciliationId_LineNumber",
                schema: "banking",
                table: "BankStatementLines",
                columns: new[] { "BankReconciliationId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedBankTransactionId",
                schema: "banking",
                table: "BankStatementLines",
                column: "MatchedBankTransactionId",
                unique: true,
                filter: "[MatchedBankTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankStatementLines",
                schema: "banking");

            migrationBuilder.DropTable(
                name: "BankReconciliations",
                schema: "banking");
        }
    }
}
