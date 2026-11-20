using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestActionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationNote",
                schema: "banking",
                table: "InternalPaymentRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                schema: "banking",
                table: "InternalPaymentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledDate",
                schema: "banking",
                table: "InternalPaymentRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionNote",
                schema: "banking",
                table: "InternalPaymentRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                schema: "banking",
                table: "InternalPaymentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationNote",
                schema: "banking",
                table: "InternalPaymentRequests");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                schema: "banking",
                table: "InternalPaymentRequests");

            migrationBuilder.DropColumn(
                name: "CancelledDate",
                schema: "banking",
                table: "InternalPaymentRequests");

            migrationBuilder.DropColumn(
                name: "SubmissionNote",
                schema: "banking",
                table: "InternalPaymentRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                schema: "banking",
                table: "InternalPaymentRequests");
        }
    }
}
