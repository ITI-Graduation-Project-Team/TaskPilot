using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixAiTelemetryAccuracy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AiTelemetryLogs",
                type: "decimal(20,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AddColumn<int>(
                name: "CachedPromptTokens",
                table: "AiTelemetryLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CalculationStatus",
                table: "AiTelemetryLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Legacy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedPromptTokens",
                table: "AiTelemetryLogs");

            migrationBuilder.DropColumn(
                name: "CalculationStatus",
                table: "AiTelemetryLogs");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AiTelemetryLogs",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,12)");
        }
    }
}
