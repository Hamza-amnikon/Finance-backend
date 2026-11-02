using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingService.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialChartOfAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accounting");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NormalBalance = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ParentAccountId = table.Column<int>(type: "int", nullable: true),
                    IsPostingAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsControlAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.UniqueConstraint("AK_Accounts_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_Accounts_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_Accounts_Parent", "[ParentAccountId] IS NULL OR [ParentAccountId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId_CompanyId",
                        columns: x => new { x.ParentAccountId, x.CompanyId },
                        principalSchema: "accounting",
                        principalTable: "Accounts",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "expenses",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId_Code",
                schema: "accounting",
                table: "Accounts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId_CompanyId",
                schema: "accounting",
                table: "Accounts",
                columns: new[] { "ParentAccountId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "accounting");
        }
    }
}
