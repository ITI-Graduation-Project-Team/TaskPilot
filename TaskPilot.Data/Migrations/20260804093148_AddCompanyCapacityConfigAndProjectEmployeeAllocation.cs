using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyCapacityConfigAndProjectEmployeeAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AllocationPercentage",
                table: "ProjectEmployees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultCapacityBufferPercentage",
                table: "Companies",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WorkingDaysMask",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkingHoursPerDay",
                table: "Companies",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllocationPercentage",
                table: "ProjectEmployees");

            migrationBuilder.DropColumn(
                name: "DefaultCapacityBufferPercentage",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "WorkingDaysMask",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "WorkingHoursPerDay",
                table: "Companies");
        }
    }
}
