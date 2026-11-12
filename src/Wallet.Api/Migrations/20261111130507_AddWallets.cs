using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.WalletApi.Migrations
{
    /// <inheritdoc />
    public partial class AddWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wallets",
                schema: "wallet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.CheckConstraint("CK_Wallets_Balance_NonNegative", "[Balance] >= 0");
                    table.ForeignKey(
                        name: "FK_Wallets_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "wallet",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopUps",
                schema: "wallet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUps", x => x.Id);
                    table.CheckConstraint("CK_TopUps_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_TopUps_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "wallet",
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TopUps_WalletId",
                schema: "wallet",
                table: "TopUps",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId",
                schema: "wallet",
                table: "Wallets",
                column: "UserId",
                unique: true);

            // Users that existed before this migration (e.g. the seeded admin) get an empty wallet.
            migrationBuilder.Sql(@"
INSERT INTO wallet.Wallets (UserId, Balance, Currency, CreatedAt, LastUpdate, CreatedBy, LastUpdatedBy)
SELECT u.Id, 0, 'IQD', SYSUTCDATETIME(), SYSUTCDATETIME(), 'migration', 'migration'
FROM wallet.Users AS u
WHERE NOT EXISTS (SELECT 1 FROM wallet.Wallets AS w WHERE w.UserId = u.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TopUps",
                schema: "wallet");

            migrationBuilder.DropTable(
                name: "Wallets",
                schema: "wallet");
        }
    }
}
