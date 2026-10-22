using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchPaymentBanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentBankingLinks",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourcePaymentId = table.Column<int>(type: "int", nullable: false),
                    SourceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BankTransactionId = table.Column<int>(type: "int", nullable: true),
                    ReversalTransactionId = table.Column<int>(type: "int", nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBankingLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentBankingLinks_BankTransactions_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBankingLinks_BankTransactions_ReversalTransactionId",
                        column: x => x.ReversalTransactionId,
                        principalSchema: "banking",
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentBankingSettings",
                schema: "banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SourceAfterUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBankingSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBankingLinks_BankTransactionId",
                schema: "banking",
                table: "PaymentBankingLinks",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBankingLinks_ReversalTransactionId",
                schema: "banking",
                table: "PaymentBankingLinks",
                column: "ReversalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBankingLinks_Source_SourcePaymentId",
                schema: "banking",
                table: "PaymentBankingLinks",
                columns: new[] { "Source", "SourcePaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBankingLinks_State_NextAttemptAtUtc",
                schema: "banking",
                table: "PaymentBankingLinks",
                columns: new[] { "State", "NextAttemptAtUtc" });
            // Activation boundary is persisted once. Existing payment history is not replayed.
            migrationBuilder.Sql(
                "INSERT INTO [banking].[PaymentBankingSettings] ([Id], [SourceAfterUtc]) VALUES (1, SYSUTCDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentBankingLinks",
                schema: "banking");

            migrationBuilder.DropTable(
                name: "PaymentBankingSettings",
                schema: "banking");
        }
    }
}
