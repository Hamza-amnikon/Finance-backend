using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "banking");


            migrationBuilder.CreateTable(
                name: "BankTransactions",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReversalOfTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.Id);
                    table.CheckConstraint("CK_BankTransaction_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_BankTransaction_Currency", "LEN([Currency]) = 3");
                    table.CheckConstraint("CK_BankTransaction_CurrencyDecimalPlaces", "[CurrencyDecimalPlaces] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_BankTransaction_Direction", "[Direction] IN (1, 2)");
                    table.CheckConstraint("CK_BankTransaction_Status", "[Status] IN ('Draft', 'Posted', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankAccounts_BankAccountId_CompanyId",
                        columns: x => new { x.BankAccountId, x.CompanyId },
                        principalSchema: "expenses",
                        principalTable: "BankAccounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankTransactions_ReversalOfTransactionId",
                        column: x => x.ReversalOfTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankAccountId_CompanyId",
                schema: "banking",
                table: "BankTransactions",
                columns: new[] { "BankAccountId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankAccountId_TransactionDate",
                schema: "banking",
                table: "BankTransactions",
                columns: new[] { "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_CompanyId_TransactionDate",
                schema: "banking",
                table: "BankTransactions",
                columns: new[] { "CompanyId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_RequestId",
                schema: "banking",
                table: "BankTransactions",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_ReversalOfTransactionId",
                schema: "banking",
                table: "BankTransactions",
                column: "ReversalOfTransactionId",
                unique: true,
                filter: "[ReversalOfTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransactions",
                schema: "banking");

            
        }
    }
}
