using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayToSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceledAt",
                table: "UserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gateway",
                table: "UserSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GatewayCustomerId",
                table: "UserSubscriptions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewaySubscriptionId",
                table: "UserSubscriptions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_GatewaySubscriptionId",
                table: "UserSubscriptions",
                column: "GatewaySubscriptionId",
                unique: true,
                filter: "[GatewaySubscriptionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_GatewaySubscriptionId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanceledAt",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "GatewayCustomerId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "GatewaySubscriptionId",
                table: "UserSubscriptions");
        }
    }
}
