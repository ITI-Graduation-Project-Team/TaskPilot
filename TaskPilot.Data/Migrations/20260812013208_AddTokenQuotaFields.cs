using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenQuotaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxTokensPerMonth",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 5000000);

            migrationBuilder.AddColumn<long>(
                name: "CurrentTokensUsedThisMonth",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxTokensPerMonth",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "CurrentTokensUsedThisMonth",
                table: "AspNetUsers");
        }
    }
}
