using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskPilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class WalletSystemImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_UserSubscriptions_UserSubscriptionId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Payments_UserSubscriptionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "Payments");

            migrationBuilder.CreateTable(
                name: "BillingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricePerMillionTokens = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingSettings_AspNetUsers_UpdatedByAdminId",
                        column: x => x.UpdatedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wallets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokensConsumed = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_AspNetUsers_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Payments_RelatedPaymentId",
                        column: x => x.RelatedPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingSettings_UpdatedByAdminId",
                table: "BillingSettings",
                column: "UpdatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CompanyId",
                table: "Wallets",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_IsDeleted",
                table: "Wallets",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_IsDeleted",
                table: "WalletTransactions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_RelatedPaymentId",
                table: "WalletTransactions",
                column: "RelatedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_TriggeredByUserId",
                table: "WalletTransactions",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId",
                table: "WalletTransactions",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingSettings");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnnualPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "EGP"),
                    HasAdvancedAnalytics = table.Column<bool>(type: "bit", nullable: false),
                    HasAi = table.Column<bool>(type: "bit", nullable: false),
                    HasTrial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxProjects = table.Column<int>(type: "int", nullable: false),
                    MaxUsersPerProject = table.Column<int>(type: "int", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    BillingCycle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    CanceledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GatewayCustomerId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GatewaySubscriptionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsTrial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TrialEndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_AspNetUsers_ProjectManagerId",
                        column: x => x.ProjectManagerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserSubscriptionId",
                table: "Payments",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IsDeleted",
                table: "SubscriptionPlans",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                table: "SubscriptionPlans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_GatewaySubscriptionId",
                table: "UserSubscriptions",
                column: "GatewaySubscriptionId",
                unique: true,
                filter: "[GatewaySubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_IsDeleted",
                table: "UserSubscriptions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PM_Pending",
                table: "UserSubscriptions",
                column: "ProjectManagerId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_ProjectManagerId",
                table: "UserSubscriptions",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Status",
                table: "UserSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SubscriptionPlanId",
                table: "UserSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_UserSubscriptions_UserSubscriptionId",
                table: "Payments",
                column: "UserSubscriptionId",
                principalTable: "UserSubscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
