using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage2_EgpToUsdConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillingSettings_AspNetUsers_UpdatedByAdminId",
                table: "BillingSettings");

            migrationBuilder.AddColumn<decimal>(
                name: "ChargedAmountEgp",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EgpPerUsdExchangeRate",
                table: "BillingSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExchangeRateUpdatedAt",
                table: "BillingSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateUpdatedByAdminId",
                table: "BillingSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BillingSettings",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "EgpPerUsdExchangeRate", "ExchangeRateUpdatedAt", "ExchangeRateUpdatedByAdminId" },
                values: new object[] { 50.00m, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_BillingSettings_ExchangeRateUpdatedByAdminId",
                table: "BillingSettings",
                column: "ExchangeRateUpdatedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillingSettings_AspNetUsers_ExchangeRateUpdatedByAdminId",
                table: "BillingSettings",
                column: "ExchangeRateUpdatedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BillingSettings_AspNetUsers_UpdatedByAdminId",
                table: "BillingSettings",
                column: "UpdatedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillingSettings_AspNetUsers_ExchangeRateUpdatedByAdminId",
                table: "BillingSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_BillingSettings_AspNetUsers_UpdatedByAdminId",
                table: "BillingSettings");

            migrationBuilder.DropIndex(
                name: "IX_BillingSettings_ExchangeRateUpdatedByAdminId",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "ChargedAmountEgp",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EgpPerUsdExchangeRate",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "ExchangeRateUpdatedAt",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "ExchangeRateUpdatedByAdminId",
                table: "BillingSettings");

            migrationBuilder.AddForeignKey(
                name: "FK_BillingSettings_AspNetUsers_UpdatedByAdminId",
                table: "BillingSettings",
                column: "UpdatedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
