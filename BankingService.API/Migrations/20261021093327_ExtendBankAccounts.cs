using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    public partial class ExtendBankAccounts : Migration
    {
        protected override void Up(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "expenses",
                table: "BankAccounts",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                schema: "expenses",
                table: "BankAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimalPlaces",
                schema: "expenses",
                table: "BankAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                schema: "expenses",
                table: "BankAccounts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpeningBalanceDate",
                schema: "expenses",
                table: "BankAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "expenses",
                table: "BankAccounts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                schema: "expenses",
                table: "BankAccounts",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                schema: "expenses",
                table: "BankAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "expenses",
                table: "BankAccounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.Sql(
                """
                UPDATE account
                SET account.CurrencyDecimalPlaces = currency.DecimalPlaces
                FROM expenses.BankAccounts AS account
                INNER JOIN sales.CurrencyMasters AS currency
                    ON currency.CurrencyCode = account.Currency;

                IF EXISTS
                (
                    SELECT 1
                    FROM expenses.BankAccounts
                    WHERE CurrencyDecimalPlaces IS NULL
                       OR CurrencyDecimalPlaces < 0
                       OR CurrencyDecimalPlaces > 4
                )
                BEGIN
                    THROW 50001,
                        'Every existing bank account must have a currency with supported decimal places in Currency Master.',
                        1;
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CurrencyDecimalPlaces",
                schema: "expenses",
                table: "BankAccounts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CurrencyDecimalPlaces",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceDate",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                schema: "expenses",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "expenses",
                table: "BankAccounts");
        }
    }
}