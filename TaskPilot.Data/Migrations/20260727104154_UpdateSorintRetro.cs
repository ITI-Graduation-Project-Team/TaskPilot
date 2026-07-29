using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSorintRetro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionItemsAr",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "ActionItemsEn",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "ActualHours",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "ChallengesAr",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "ChallengesEn",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "TeamSentimentSummaryAr",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "TeamSentimentSummaryEn",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "WhatWentWellAr",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "WhatWentWellEn",
                table: "SprintRetrospectives");

            migrationBuilder.RenameColumn(
                name: "ExpectedHours",
                table: "SprintRetrospectives",
                newName: "TotalEstimatedHours");

            migrationBuilder.RenameColumn(
                name: "EstimationAccuracy",
                table: "SprintRetrospectives",
                newName: "TotalActualHours");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisJson",
                table: "SprintRetrospectives",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CompletedTasks",
                table: "SprintRetrospectives",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAt",
                table: "SprintRetrospectives",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ImprovementsJson",
                table: "SprintRetrospectives",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalTasks",
                table: "SprintRetrospectives",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnfinishedTasks",
                table: "SprintRetrospectives",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "VelocityRatio",
                table: "SprintRetrospectives",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisJson",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "CompletedTasks",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "ImprovementsJson",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "TotalTasks",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "UnfinishedTasks",
                table: "SprintRetrospectives");

            migrationBuilder.DropColumn(
                name: "VelocityRatio",
                table: "SprintRetrospectives");

            migrationBuilder.RenameColumn(
                name: "TotalEstimatedHours",
                table: "SprintRetrospectives",
                newName: "ExpectedHours");

            migrationBuilder.RenameColumn(
                name: "TotalActualHours",
                table: "SprintRetrospectives",
                newName: "EstimationAccuracy");

            migrationBuilder.AddColumn<string>(
                name: "ActionItemsAr",
                table: "SprintRetrospectives",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActionItemsEn",
                table: "SprintRetrospectives",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualHours",
                table: "SprintRetrospectives",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ChallengesAr",
                table: "SprintRetrospectives",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChallengesEn",
                table: "SprintRetrospectives",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeamSentimentSummaryAr",
                table: "SprintRetrospectives",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeamSentimentSummaryEn",
                table: "SprintRetrospectives",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatWentWellAr",
                table: "SprintRetrospectives",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatWentWellEn",
                table: "SprintRetrospectives",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }
    }
}
