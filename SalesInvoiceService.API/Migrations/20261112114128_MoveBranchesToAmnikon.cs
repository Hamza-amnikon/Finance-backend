using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesInvoiceService.API.Migrations
{
    /// <inheritdoc />
    public partial class MoveBranchesToAmnikon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "amnikon");

            migrationBuilder.RenameTable(
                name: "BranchMasters",
                schema: "sales",
                newName: "Branches",
                newSchema: "amnikon");

            migrationBuilder.RenameIndex(
                name: "IX_BranchMasters_Name",
                schema: "amnikon",
                table: "Branches",
                newName: "IX_Branches_Name");

            // Older running services resolve the same rows during deployment.
            migrationBuilder.Sql(
                "CREATE SYNONYM [sales].[BranchMasters] FOR [amnikon].[Branches];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SYNONYM [sales].[BranchMasters];");

            migrationBuilder.RenameTable(
                name: "Branches",
                schema: "amnikon",
                newName: "BranchMasters",
                newSchema: "sales");

            migrationBuilder.RenameIndex(
                name: "IX_Branches_Name",
                schema: "sales",
                table: "BranchMasters",
                newName: "IX_BranchMasters_Name");
        }
    }
}
