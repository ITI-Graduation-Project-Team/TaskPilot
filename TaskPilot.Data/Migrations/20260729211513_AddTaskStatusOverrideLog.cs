using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskStatusOverrideLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<string>(
            //    name: "ActionItemsAr",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(4000)",
            //    maxLength: 4000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<string>(
            //    name: "ActionItemsEn",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(4000)",
            //    maxLength: 4000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<decimal>(
            //    name: "ActualHours",
            //    table: "SprintRetrospectives",
            //    type: "decimal(18,2)",
            //    nullable: false,
            //    defaultValue: 0m);

            //migrationBuilder.AddColumn<string>(
            //    name: "ChallengesAr",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(4000)",
            //    maxLength: 4000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<string>(
            //    name: "ChallengesEn",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(4000)",
            //    maxLength: 4000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<decimal>(
            //    name: "EstimationAccuracy",
            //    table: "SprintRetrospectives",
            //    type: "decimal(18,2)",
            //    nullable: false,
            //    defaultValue: 0m);

            //migrationBuilder.AddColumn<decimal>(
            //    name: "ExpectedHours",
            //    table: "SprintRetrospectives",
            //    type: "decimal(18,2)",
            //    nullable: false,
            //    defaultValue: 0m);

            //migrationBuilder.AddColumn<string>(
            //    name: "TeamSentimentSummaryAr",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(1000)",
            //    maxLength: 1000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<string>(
            //    name: "TeamSentimentSummaryEn",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(1000)",
            //    maxLength: 1000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<string>(
            //    name: "WhatWentWellAr",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(4000)",
            //    maxLength: 4000,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<string>(
            //    name: "WhatWentWellEn",
            //    table: "SprintRetrospectives",
            //    type: "nvarchar(4000)",
            //    maxLength: 4000,
            //    nullable: false,
            //    defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TaskStatusOverrideLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedByPmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ReasonEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReasonAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverrideType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatusOverrideLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskStatusOverrideLogs_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusOverrideLogs_TaskId",
                table: "TaskStatusOverrideLogs",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskStatusOverrideLogs");

            //migrationBuilder.DropColumn(
            //    name: "ActionItemsAr",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "ActionItemsEn",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "ActualHours",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "ChallengesAr",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "ChallengesEn",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "EstimationAccuracy",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "ExpectedHours",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "TeamSentimentSummaryAr",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "TeamSentimentSummaryEn",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "WhatWentWellAr",
            //    table: "SprintRetrospectives");

            //migrationBuilder.DropColumn(
            //    name: "WhatWentWellEn",
            //    table: "SprintRetrospectives");
        }
    }
}
