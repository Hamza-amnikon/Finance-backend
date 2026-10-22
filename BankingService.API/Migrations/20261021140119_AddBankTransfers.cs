using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTransfers",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceBankAccountId = table.Column<int>(type: "int", nullable: false),
                    SourceCompanyId = table.Column<int>(type: "int", nullable: false),
                    DestinationBankAccountId = table.Column<int>(type: "int", nullable: false),
                    DestinationCompanyId = table.Column<int>(type: "int", nullable: false),
                    SourceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SourceCurrencyDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    DestinationCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DestinationCurrencyDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    SourceAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SourceFeeAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DestinationAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EffectiveExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SourceTransactionId = table.Column<int>(type: "int", nullable: true),
                    SourceFeeTransactionId = table.Column<int>(type: "int", nullable: true),
                    DestinationTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransfers", x => x.Id);
                    table.CheckConstraint("CK_BankTransfer_Accounts", "[SourceBankAccountId] <> [DestinationBankAccountId]");
                    table.CheckConstraint("CK_BankTransfer_Currencies", "LEN([SourceCurrency]) = 3 AND LEN([DestinationCurrency]) = 3");
                    table.CheckConstraint("CK_BankTransfer_CurrencyDecimalPlaces", "[SourceCurrencyDecimalPlaces] BETWEEN 0 AND 4 AND [DestinationCurrencyDecimalPlaces] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_BankTransfer_DestinationAmount", "[DestinationAmount] IS NULL OR [DestinationAmount] > 0");
                    table.CheckConstraint("CK_BankTransfer_ExchangeRate", "[EffectiveExchangeRate] IS NULL OR [EffectiveExchangeRate] > 0");
                    table.CheckConstraint("CK_BankTransfer_SourceAmount", "[SourceAmount] > 0");
                    table.CheckConstraint("CK_BankTransfer_SourceFee", "[SourceFeeAmount] >= 0");
                    table.CheckConstraint("CK_BankTransfer_Status", "[Status] IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_BankTransfers_BankAccounts_DestinationBankAccountId_DestinationCompanyId",
                        columns: x => new { x.DestinationBankAccountId, x.DestinationCompanyId },
                        principalSchema: "expenses",
                        principalTable: "BankAccounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransfers_BankAccounts_SourceBankAccountId_SourceCompanyId",
                        columns: x => new { x.SourceBankAccountId, x.SourceCompanyId },
                        principalSchema: "expenses",
                        principalTable: "BankAccounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransfers_BankTransactions_DestinationTransactionId",
                        column: x => x.DestinationTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransfers_BankTransactions_SourceFeeTransactionId",
                        column: x => x.SourceFeeTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransfers_BankTransactions_SourceTransactionId",
                        column: x => x.SourceTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_DestinationBankAccountId_DestinationCompanyId",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "DestinationBankAccountId", "DestinationCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_DestinationBankAccountId_TransferDate",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "DestinationBankAccountId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_DestinationCompanyId_TransferDate",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "DestinationCompanyId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_DestinationTransactionId",
                schema: "banking",
                table: "BankTransfers",
                column: "DestinationTransactionId",
                unique: true,
                filter: "[DestinationTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_RequestId",
                schema: "banking",
                table: "BankTransfers",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_SourceBankAccountId_SourceCompanyId",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "SourceBankAccountId", "SourceCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_SourceBankAccountId_TransferDate",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "SourceBankAccountId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_SourceCompanyId_TransferDate",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "SourceCompanyId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_SourceFeeTransactionId",
                schema: "banking",
                table: "BankTransfers",
                column: "SourceFeeTransactionId",
                unique: true,
                filter: "[SourceFeeTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_SourceTransactionId",
                schema: "banking",
                table: "BankTransfers",
                column: "SourceTransactionId",
                unique: true,
                filter: "[SourceTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_Status_TransferDate",
                schema: "banking",
                table: "BankTransfers",
                columns: new[] { "Status", "TransferDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransfers",
                schema: "banking");
        }
    }
}
