using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintBurnoutSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SprintBurnoutSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BurnoutScore = table.Column<int>(type: "int", nullable: false),
                    WorkloadScore = table.Column<int>(type: "int", nullable: false),
                    PaceScore = table.Column<int>(type: "int", nullable: false),
                    EngagementScore = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrendDirection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintBurnoutSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintBurnoutSnapshots_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintBurnoutSnapshots_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SprintBurnoutSnapshots_EmployeeId",
                table: "SprintBurnoutSnapshots",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintBurnoutSnapshots_SprintId",
                table: "SprintBurnoutSnapshots",
                column: "SprintId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SprintBurnoutSnapshots");
        }
    }
}
