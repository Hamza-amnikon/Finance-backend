using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedPaymentVoiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Recorded");

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedDate",
                schema: "sales",
                table: "SalesInvoicePayments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "sales",
                table: "SalesInvoicePayments");

            migrationBuilder.DropColumn(
                name: "VoidedDate",
                schema: "sales",
                table: "SalesInvoicePayments");
        }
    }
}
