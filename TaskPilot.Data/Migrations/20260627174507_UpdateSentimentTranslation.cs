using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSentimentTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TeamSentimentSummary",
                table: "SprintRetrospectives",
                newName: "TeamSentimentSummaryEn");

            migrationBuilder.AddColumn<string>(
                name: "TeamSentimentSummaryAr",
                table: "SprintRetrospectives",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamSentimentSummaryAr",
                table: "SprintRetrospectives");

            migrationBuilder.RenameColumn(
                name: "TeamSentimentSummaryEn",
                table: "SprintRetrospectives",
                newName: "TeamSentimentSummary");
        }
    }
}
