using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTechStack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlatformTargets",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ProjectType",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TechStack",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformTargets",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectType",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TechStack",
                table: "Projects");
        }
    }
}
