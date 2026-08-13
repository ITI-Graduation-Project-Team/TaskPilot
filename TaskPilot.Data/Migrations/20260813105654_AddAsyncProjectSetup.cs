using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAsyncProjectSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectSetupStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechStackStatus = table.Column<int>(type: "int", nullable: false),
                    TechStackSuggestionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechStackError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WbsStatus = table.Column<int>(type: "int", nullable: false),
                    WbsJobId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WbsAttemptCount = table.Column<int>(type: "int", nullable: false),
                    UserStoriesCreated = table.Column<int>(type: "int", nullable: false),
                    TasksCreated = table.Column<int>(type: "int", nullable: false),
                    WbsStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WbsCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WbsError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SkillsStatus = table.Column<int>(type: "int", nullable: false),
                    SkillsJobId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkillsAttemptCount = table.Column<int>(type: "int", nullable: false),
                    TasksProcessed = table.Column<int>(type: "int", nullable: false),
                    TasksEnriched = table.Column<int>(type: "int", nullable: false),
                    TasksSkipped = table.Column<int>(type: "int", nullable: false),
                    SkillsCreated = table.Column<int>(type: "int", nullable: false),
                    SkillsStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkillsCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkillsError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSetupStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSetupStates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSetupStates_IsDeleted",
                table: "ProjectSetupStates",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSetupStates_ProjectId",
                table: "ProjectSetupStates",
                column: "ProjectId",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ProjectSetupStates
                    (Id, ProjectId, TechStackStatus, WbsStatus, SkillsStatus,
                     WbsAttemptCount, UserStoriesCreated, TasksCreated,
                     SkillsAttemptCount, TasksProcessed, TasksEnriched, TasksSkipped, SkillsCreated,
                     WbsCompletedAt, SkillsCompletedAt, CreatedAt, IsDeleted)
                SELECT
                    NEWID(), p.Id,
                    CASE WHEN p.TechStack IS NOT NULL AND p.TechStack <> N'[]' THEN 2 ELSE 0 END,
                    CASE WHEN EXISTS (SELECT 1 FROM UserStories us WHERE us.ProjectId = p.Id AND us.IsDeleted = 0) THEN 3 ELSE 0 END,
                    CASE WHEN EXISTS (SELECT 1 FROM UserStories us WHERE us.ProjectId = p.Id AND us.IsDeleted = 0) THEN 3 ELSE 0 END,
                    0,
                    (SELECT COUNT(*) FROM UserStories us WHERE us.ProjectId = p.Id AND us.IsDeleted = 0),
                    (SELECT COUNT(*) FROM Tasks t INNER JOIN UserStories us ON t.UserStoryId = us.Id WHERE us.ProjectId = p.Id AND t.IsDeleted = 0 AND us.IsDeleted = 0),
                    0, 0, 0, 0, 0,
                    CASE WHEN EXISTS (SELECT 1 FROM UserStories us WHERE us.ProjectId = p.Id AND us.IsDeleted = 0) THEN GETUTCDATE() ELSE NULL END,
                    CASE WHEN EXISTS (SELECT 1 FROM UserStories us WHERE us.ProjectId = p.Id AND us.IsDeleted = 0) THEN GETUTCDATE() ELSE NULL END,
                    GETUTCDATE(), 0
                FROM Projects p
                WHERE p.IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectSetupStates");
        }
    }
}
