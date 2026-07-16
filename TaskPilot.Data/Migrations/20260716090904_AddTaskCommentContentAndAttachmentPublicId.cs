using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCommentContentAndAttachmentPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentAr",
                table: "TaskComments");

            migrationBuilder.RenameColumn(
                name: "ContentEn",
                table: "TaskComments",
                newName: "Content");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "TaskAttachments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "TaskAttachments");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "TaskComments",
                newName: "ContentEn");

            migrationBuilder.AddColumn<string>(
                name: "ContentAr",
                table: "TaskComments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
