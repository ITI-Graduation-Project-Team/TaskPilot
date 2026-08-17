using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsPaginationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Incidental schema drift detected by EF scaffolding — keep as-is.
            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "UserSkills",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldDefaultValue: "Intermediate");

            // Composite covering index for the paginated company-projects query.
            //
            // Query pattern:
            //   WHERE CompanyId = @c AND ManagerId = @m AND IsDeleted = 0
            //         [AND Status = @s | AND Status IN (0,1)]
            //   ORDER BY CreatedAt DESC
            //   OFFSET @skip ROWS FETCH NEXT 6 ROWS ONLY
            //
            // Column order rationale:
            //   1. CompanyId  — highest-selectivity equality predicate (narrow set)
            //   2. ManagerId  — equality predicate (one PM per company scope)
            //   3. IsDeleted  — always = 0; placed before CreatedAt so it acts as
            //                   a range gate before the sort key
            //   4. CreatedAt  — sort key; DESC matches ORDER BY, avoiding Sort operator
            //   5. Status     — conditional filter; at the end so it covers all
            //                   statusFilter variants without blocking the sort key
            //
            // The existing IX_Projects_CompanyId_ManagerId index (CompanyId, ManagerId)
            // does NOT include IsDeleted, CreatedAt, or Status, so SQL Server cannot
            // satisfy the full predicate + sort + OFFSET from it alone.
            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_ManagerId_IsDeleted_CreatedAt_Status",
                table: "Projects",
                columns: new[] { "CompanyId", "ManagerId", "IsDeleted", "CreatedAt", "Status" },
                descending: new[] { false, false, false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_CompanyId_ManagerId_IsDeleted_CreatedAt_Status",
                table: "Projects");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "UserSkills",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "Intermediate",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
