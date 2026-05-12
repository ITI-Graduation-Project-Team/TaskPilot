using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class addSkillAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskRequiredSkills_Skills_SkillId",
                table: "TaskRequiredSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkills_Skills_SkillId",
                table: "UserSkills");

            migrationBuilder.DropIndex(
                name: "IX_Skills_Name",
                table: "Skills");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "UserSkills",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "Intermediate",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceScore",
                table: "UserSkills",
                type: "float(5)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "UserSkills",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "RequiredLevel",
                table: "TaskRequiredSkills",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Skills",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "AvailabilityStatus",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeniorityLevel",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalYearsOfExperience",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SkillAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SkillId = table.Column<int>(type: "int", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillAliases_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSkills_SkillId_Level",
                table: "UserSkills",
                columns: new[] { "SkillId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_NormalizedName",
                table: "Skills",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_JobTitle",
                table: "AspNetUsers",
                column: "JobTitle");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SeniorityLevel",
                table: "AspNetUsers",
                column: "SeniorityLevel");

            migrationBuilder.CreateIndex(
                name: "IX_SkillAliases_Alias",
                table: "SkillAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillAliases_SkillId",
                table: "SkillAliases",
                column: "SkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRequiredSkills_Skills_SkillId",
                table: "TaskRequiredSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkills_Skills_SkillId",
                table: "UserSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskRequiredSkills_Skills_SkillId",
                table: "TaskRequiredSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkills_Skills_SkillId",
                table: "UserSkills");

            migrationBuilder.DropTable(
                name: "SkillAliases");

            migrationBuilder.DropIndex(
                name: "IX_UserSkills_SkillId_Level",
                table: "UserSkills");

            migrationBuilder.DropIndex(
                name: "IX_Skills_Name",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Skills_NormalizedName",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_JobTitle",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SeniorityLevel",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "UserSkills");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "UserSkills");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SeniorityLevel",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TotalYearsOfExperience",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "UserSkills",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldDefaultValue: "Intermediate");

            migrationBuilder.AlterColumn<int>(
                name: "RequiredLevel",
                table: "TaskRequiredSkills",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "AvailabilityStatus",
                table: "AspNetUsers",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRequiredSkills_Skills_SkillId",
                table: "TaskRequiredSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkills_Skills_SkillId",
                table: "UserSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
