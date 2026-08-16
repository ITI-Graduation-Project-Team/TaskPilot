using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageLimitTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Policies",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LogoFileSize",
                table: "Companies",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "AvatarFileSize",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CurrentStorageUsedBytes",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CvFileSize",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE us
                SET CurrentStorageUsedBytes = (
                    SELECT ISNULL(SUM(ta.FileSize), 0)
                    FROM TaskAttachments ta
                    INNER JOIN Tasks t ON ta.TaskId = t.Id
                    LEFT JOIN UserStories us2 ON t.UserStoryId = us2.Id
                    LEFT JOIN Sprints s ON t.SprintId = s.Id
                    INNER JOIN Projects p ON p.Id = ISNULL(us2.ProjectId, s.ProjectId)
                    WHERE p.ManagerId = us.Id
                )
                FROM AspNetUsers us
                WHERE us.UserType = 'ProjectManager'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "LogoFileSize",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AvatarFileSize",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CurrentStorageUsedBytes",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CvFileSize",
                table: "AspNetUsers");
        }
    }
}
