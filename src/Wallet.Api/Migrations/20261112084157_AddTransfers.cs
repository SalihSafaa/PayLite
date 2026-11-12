using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.WalletApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transfers",
                schema: "wallet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromWalletId = table.Column<int>(type: "int", nullable: false),
                    ToWalletId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfers_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_Transfers_DifferentWallets", "[FromWalletId] <> [ToWalletId]");
                    table.ForeignKey(
                        name: "FK_Transfers_Wallets_FromWalletId",
                        column: x => x.FromWalletId,
                        principalSchema: "wallet",
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Wallets_ToWalletId",
                        column: x => x.ToWalletId,
                        principalSchema: "wallet",
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FromWalletId_CreatedAt",
                schema: "wallet",
                table: "Transfers",
                columns: new[] { "FromWalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ToWalletId_CreatedAt",
                schema: "wallet",
                table: "Transfers",
                columns: new[] { "ToWalletId", "CreatedAt" });

            migrationBuilder.Sql(@"
CREATE PROCEDURE wallet.usp_Transfer
    @FromUserId  INT,
    @ToUsername  NVARCHAR(50),
    @Amount      DECIMAL(18,2),
    @CreatedBy   NVARCHAR(256),
    @TransferId  BIGINT        OUTPUT,
    @NewBalance  DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    -- Any error rolls back the WHOLE transaction (no half-done transfer can be committed).
    SET XACT_ABORT ON;

    IF @Amount IS NULL OR @Amount <= 0
        THROW 50003, 'Amount must be greater than zero.', 1;

    -- Find both wallets. Wallet ids never change, so this lookup needs no lock.
    DECLARE @FromWalletId INT, @ToWalletId INT;

    SELECT @FromWalletId = Id FROM wallet.Wallets WHERE UserId = @FromUserId;

    SELECT @ToWalletId = w.Id
    FROM wallet.Wallets AS w
    JOIN wallet.Users AS u ON u.Id = w.UserId
    WHERE u.Username = @ToUsername;

    IF @FromWalletId IS NULL OR @ToWalletId IS NULL
        THROW 50002, 'Wallet not found.', 1;

    IF @FromWalletId = @ToWalletId
        THROW 50003, 'You cannot send money to yourself.', 1;

    -- Join the caller's transaction if there is one, otherwise own it.
    DECLARE @OwnsTran BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    IF @OwnsTran = 1 BEGIN TRAN;

    -- DEADLOCK PREVENTION: always lock the two wallets in the same order (lowest id first).
    -- If A->B locked A then B while B->A locked B then A, each would wait for the other
    -- and SQL Server would kill one with error 1205. With a fixed order, the second
    -- transfer simply waits for the first to finish.
    DECLARE @FirstId  INT = IIF(@FromWalletId < @ToWalletId, @FromWalletId, @ToWalletId);
    DECLARE @SecondId INT = IIF(@FromWalletId < @ToWalletId, @ToWalletId, @FromWalletId);
    DECLARE @Ignore DECIMAL(18,2), @FromBalance DECIMAL(18,2);

    SELECT @Ignore = Balance FROM wallet.Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @FirstId;
    SELECT @Ignore = Balance FROM wallet.Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @SecondId;

    -- Both rows are now locked by us until COMMIT, so this balance cannot change under us.
    SELECT @FromBalance = Balance FROM wallet.Wallets WHERE Id = @FromWalletId;

    IF @FromBalance < @Amount
        THROW 50001, 'Insufficient funds.', 1;

    UPDATE wallet.Wallets
    SET Balance = Balance - @Amount, LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = @CreatedBy
    WHERE Id = @FromWalletId;

    UPDATE wallet.Wallets
    SET Balance = Balance + @Amount, LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = @CreatedBy
    WHERE Id = @ToWalletId;

    INSERT INTO wallet.Transfers (FromWalletId, ToWalletId, Amount, Currency, CreatedAt, CreatedBy)
    VALUES (@FromWalletId, @ToWalletId, @Amount, 'IQD', SYSUTCDATETIME(), @CreatedBy);

    SET @TransferId = SCOPE_IDENTITY();
    SET @NewBalance = @FromBalance - @Amount;

    IF @OwnsTran = 1 COMMIT;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS wallet.usp_Transfer;");

            migrationBuilder.DropTable(
                name: "Transfers",
                schema: "wallet");
        }
    }
}
