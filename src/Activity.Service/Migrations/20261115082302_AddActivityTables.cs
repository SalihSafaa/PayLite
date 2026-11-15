using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.ActivityService.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "activity");

            migrationBuilder.CreateTable(
                name: "MerchantHourlySummaries",
                schema: "activity",
                columns: table => new
                {
                    MerchantUserId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Hour = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantHourlySummaries", x => new { x.MerchantUserId, x.Date, x.Hour });
                    table.CheckConstraint("CK_MerchantHourlySummaries_Count", "[PaymentCount] >= 0");
                    table.CheckConstraint("CK_MerchantHourlySummaries_Hour", "[Hour] BETWEEN 0 AND 23");
                    table.CheckConstraint("CK_MerchantHourlySummaries_Total", "[TotalAmount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "activity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferId = table.Column<long>(type: "bigint", nullable: false),
                    FromUsername = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_Amount_Positive", "[Amount] > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProcessedEvents",
                schema: "activity",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventId",
                schema: "activity",
                table: "Notifications",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_OccurredAt",
                schema: "activity",
                table: "Notifications",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt",
                schema: "activity",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantHourlySummaries",
                schema: "activity");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "activity");

            migrationBuilder.DropTable(
                name: "ProcessedEvents",
                schema: "activity");
        }
    }
}
