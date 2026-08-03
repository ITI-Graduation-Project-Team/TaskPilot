using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDependsOnStoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnStoryId",
                table: "UserStories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStories_DependsOnStoryId",
                table: "UserStories",
                column: "DependsOnStoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserStories_UserStories_DependsOnStoryId",
                table: "UserStories",
                column: "DependsOnStoryId",
                principalTable: "UserStories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserStories_UserStories_DependsOnStoryId",
                table: "UserStories");

            migrationBuilder.DropIndex(
                name: "IX_UserStories_DependsOnStoryId",
                table: "UserStories");

            migrationBuilder.DropColumn(
                name: "DependsOnStoryId",
                table: "UserStories");
        }
    }
}
