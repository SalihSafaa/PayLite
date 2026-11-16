using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.WalletApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAfter",
                schema: "wallet",
                table: "TopUps",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                schema: "wallet",
                table: "TopUps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                schema: "wallet",
                table: "TopUps",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "wallet",
                table: "TopUps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                // Every top-up before this migration credited the wallet straight away, so it is Approved.
                // (New rows always get an explicit status from the code; this default only fills existing ones.)
                defaultValue: "Approved");

            migrationBuilder.CreateIndex(
                name: "IX_TopUps_Status_CreatedAt",
                schema: "wallet",
                table: "TopUps",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUps_BalanceAfter_WhenApproved",
                schema: "wallet",
                table: "TopUps",
                sql: "([Status] = 'Approved' AND [BalanceAfter] IS NOT NULL) OR ([Status] <> 'Approved' AND [BalanceAfter] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUps_Status",
                schema: "wallet",
                table: "TopUps",
                sql: "[Status] IN ('Pending', 'Approved', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Before this migration a top-up row always meant "credited". Pending and Rejected requests moved no
            // money, so they are removed rather than turned into fake credits.
            migrationBuilder.Sql("DELETE FROM wallet.TopUps WHERE Status <> 'Approved';");

            migrationBuilder.DropIndex(
                name: "IX_TopUps_Status_CreatedAt",
                schema: "wallet",
                table: "TopUps");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUps_BalanceAfter_WhenApproved",
                schema: "wallet",
                table: "TopUps");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUps_Status",
                schema: "wallet",
                table: "TopUps");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                schema: "wallet",
                table: "TopUps");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                schema: "wallet",
                table: "TopUps");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "wallet",
                table: "TopUps");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAfter",
                schema: "wallet",
                table: "TopUps",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
