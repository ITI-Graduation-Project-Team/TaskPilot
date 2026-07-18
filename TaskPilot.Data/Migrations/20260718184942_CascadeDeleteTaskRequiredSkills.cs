using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteTaskRequiredSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskRequiredSkills_Tasks_TaskId",
                table: "TaskRequiredSkills");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRequiredSkills_Tasks_TaskId",
                table: "TaskRequiredSkills",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskRequiredSkills_Tasks_TaskId",
                table: "TaskRequiredSkills");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRequiredSkills_Tasks_TaskId",
                table: "TaskRequiredSkills",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
