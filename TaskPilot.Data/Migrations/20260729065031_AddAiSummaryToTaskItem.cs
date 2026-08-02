using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSummaryToTaskItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiSummaryAr",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummaryCitationsJson",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummaryEn",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSummaryAr",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AiSummaryCitationsJson",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AiSummaryEn",
                table: "Tasks");
        }
    }
}
