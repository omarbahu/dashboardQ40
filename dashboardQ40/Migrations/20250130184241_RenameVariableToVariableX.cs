using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dashboardQ40.Migrations
{
    /// <inheritdoc />
    public partial class RenameVariableToVariableX : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DashboardWidgets");

            migrationBuilder.RenameColumn(
                name: "Variable",
                table: "DashboardWidgets",
                newName: "VariableX");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VariableX",
                table: "DashboardWidgets",
                newName: "Variable");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DashboardWidgets",
                type: "datetime2",
                nullable: true);
        }
    }
}
