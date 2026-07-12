using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintRiskAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SprintRiskAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AffectedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AffectedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastDetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintRiskAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintRiskAlerts_AspNetUsers_AffectedEmployeeId",
                        column: x => x.AffectedEmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SprintRiskAlerts_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintRiskAlerts_Tasks_AffectedTaskId",
                        column: x => x.AffectedTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SprintRiskAlerts_AffectedEmployeeId",
                table: "SprintRiskAlerts",
                column: "AffectedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintRiskAlerts_AffectedTaskId",
                table: "SprintRiskAlerts",
                column: "AffectedTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintRiskAlerts_IsDismissed",
                table: "SprintRiskAlerts",
                column: "IsDismissed");

            migrationBuilder.CreateIndex(
                name: "IX_SprintRiskAlerts_LastDetectedAt",
                table: "SprintRiskAlerts",
                column: "LastDetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SprintRiskAlerts_SprintId",
                table: "SprintRiskAlerts",
                column: "SprintId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SprintRiskAlerts");
        }
    }
}
