using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowReusingDeletedProjectNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_CompanyId_NameEn",
                table: "Projects");

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT [CompanyId], UPPER(LTRIM(RTRIM([NameEn])))
                    FROM [Projects]
                    WHERE [IsDeleted] = 0
                    GROUP BY [CompanyId], UPPER(LTRIM(RTRIM([NameEn])))
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Active projects contain duplicate normalized English names. Resolve them before applying this migration.', 1;

                UPDATE [Projects]
                SET [NameEn] = LTRIM(RTRIM([NameEn]));
                """);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedNameEn",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                computedColumnSql: "UPPER(LTRIM(RTRIM([NameEn])))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_NormalizedNameEn",
                table: "Projects",
                columns: new[] { "CompanyId", "NormalizedNameEn" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_CompanyId_NormalizedNameEn",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "NormalizedNameEn",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_NameEn",
                table: "Projects",
                columns: new[] { "CompanyId", "NameEn" },
                unique: true);
        }
    }
}
