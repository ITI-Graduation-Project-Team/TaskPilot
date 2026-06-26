using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintRetrospective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SprintRetrospectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WhatWentWellEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WhatWentWellAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ChallengesEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ChallengesAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ActionItemsEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ActionItemsAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CompletionRate = table.Column<double>(type: "float", nullable: false),
                    EstimationAccuracy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TeamSentimentSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintRetrospectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintRetrospectives_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SprintRetrospectives_SprintId",
                table: "SprintRetrospectives",
                column: "SprintId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SprintRetrospectives");
        }
    }
}
