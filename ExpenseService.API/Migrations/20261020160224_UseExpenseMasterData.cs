using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseService.API.Migrations
{
    /// <inheritdoc />
    public partial class UseExpenseMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Expense_Currency",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "expenses",
                table: "Expenses",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimalPlaces",
                schema: "expenses",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RateBaseCurrency",
                schema: "expenses",
                table: "Expenses",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RateQuoteCurrency",
                schema: "expenses",
                table: "Expenses",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            // Earlier versions saved every rate as quote units for this historical pair.
            // Keep the existing numeric rates and their direction unchanged.
            migrationBuilder.Sql(
                """
                UPDATE [expenses].[Expenses]
                SET [CurrencyDecimalPlaces] = 2,
                    [RateBaseCurrency] = N'AED',
                    [RateQuoteCurrency] = N'INR';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expense_Currency",
                schema: "expenses",
                table: "Expenses",
                sql: "LEN([Currency]) = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM [expenses].[Expenses]
                    WHERE [Currency] NOT IN (N'INR', N'AED')
                       OR [RateBaseCurrency] <> N'AED'
                       OR [RateQuoteCurrency] <> N'INR'
                       OR ROUND([Amount], 2) <> [Amount]
                )
                THROW 50000, 'Existing expenses use currency pairs or precision unsupported by the previous version.', 1;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Expense_Currency",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CurrencyDecimalPlaces",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RateBaseCurrency",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RateQuoteCurrency",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "expenses",
                table: "Expenses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expense_Currency",
                schema: "expenses",
                table: "Expenses",
                sql: "[Currency] IN ('INR', 'AED')");
        }
    }
}
