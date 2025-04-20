using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dashboardQ40.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDashboardFilterReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardFilters");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DashboardTemplates");

            migrationBuilder.RenameColumn(
                name: "Line",
                table: "DashboardTemplates",
                newName: "VariableY");

            migrationBuilder.AddColumn<string>(
                name: "Variable",
                table: "DashboardWidgets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Linea",
                table: "DashboardTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Planta",
                table: "DashboardTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variable",
                table: "DashboardWidgets");

            migrationBuilder.DropColumn(
                name: "Linea",
                table: "DashboardTemplates");

            migrationBuilder.DropColumn(
                name: "Planta",
                table: "DashboardTemplates");

            migrationBuilder.RenameColumn(
                name: "VariableY",
                table: "DashboardTemplates",
                newName: "Line");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DashboardTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DashboardFilters",
                columns: table => new
                {
                    FilterID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FilterName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterOptions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardFilters", x => x.FilterID);
                    table.ForeignKey(
                        name: "FK_DashboardFilters_DashboardTemplates_TemplateID",
                        column: x => x.TemplateID,
                        principalTable: "DashboardTemplates",
                        principalColumn: "TemplateID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardFilters_TemplateID",
                table: "DashboardFilters",
                column: "TemplateID");
        }
    }
}
