using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentMadeService.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkPaymentsMadeToBanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                schema: "purchases",
                table: "PaymentsMade",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                schema: "purchases",
                table: "PaymentsMade",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BankAmount",
                schema: "purchases",
                table: "PaymentsMade",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCurrency",
                schema: "purchases",
                table: "PaymentsMade",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankReversalTransactionId",
                schema: "purchases",
                table: "PaymentsMade",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankTransactionId",
                schema: "purchases",
                table: "PaymentsMade",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementType",
                schema: "purchases",
                table: "PaymentsMade",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentsMade_BankTransactionId",
                schema: "purchases",
                table: "PaymentsMade",
                column: "BankTransactionId",
                unique: true,
                filter: "[BankTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentsMade_BankTransactionId",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "BankAccountName",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "BankAmount",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "BankCurrency",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "BankReversalTransactionId",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "BankTransactionId",
                schema: "purchases",
                table: "PaymentsMade");

            migrationBuilder.DropColumn(
                name: "SettlementType",
                schema: "purchases",
                table: "PaymentsMade");
        }
    }
}
