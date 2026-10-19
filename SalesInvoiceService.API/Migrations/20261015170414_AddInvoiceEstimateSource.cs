using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceEstimateSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SourceEstimateDueDate",
                schema: "sales",
                table: "SalesInvoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceEstimateId",
                schema: "sales",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SourceEstimateInvoiceDate",
                schema: "sales",
                table: "SalesInvoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEstimateNumber",
                schema: "sales",
                table: "SalesInvoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SourceEstimateRowVersion",
                schema: "sales",
                table: "SalesInvoices",
                type: "varbinary(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SourceEstimateId",
                schema: "sales",
                table: "SalesInvoices",
                column: "SourceEstimateId",
                unique: true,
                filter: "[SourceEstimateId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_SourceEstimateId",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SourceEstimateDueDate",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SourceEstimateId",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SourceEstimateInvoiceDate",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SourceEstimateNumber",
                schema: "sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SourceEstimateRowVersion",
                schema: "sales",
                table: "SalesInvoices");
        }
    }
}
