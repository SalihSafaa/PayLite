using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.WalletApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FromBalanceAfter",
                schema: "wallet",
                table: "Transfers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "wallet",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    TransferId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => new { x.UserId, x.IdempotencyKey });
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalSchema: "wallet",
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "wallet",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                schema: "wallet",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_TransferId",
                schema: "wallet",
                table: "IdempotencyKeys",
                column: "TransferId");

            migrationBuilder.Sql("""
CREATE OR ALTER PROCEDURE wallet.usp_Transfer
    @FromUserId      INT,
    @ToUsername      NVARCHAR(50),
    @Amount          DECIMAL(18,2),
    @CreatedBy       NVARCHAR(256),
    @IdempotencyKey  UNIQUEIDENTIFIER,
    @TransferId      BIGINT OUTPUT,
    @NewBalance      DECIMAL(18,2) OUTPUT,
    @Replayed        BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Replayed = 0;

    IF @Amount IS NULL OR @Amount <= 0 OR @Amount % 1 <> 0
        THROW 50004, 'Amount must be a positive whole number of IQD.', 1;

    DECLARE @FromWalletId INT, @ToWalletId INT;
    SELECT @FromWalletId = Id FROM wallet.Wallets WHERE UserId = @FromUserId;
    SELECT @ToWalletId = w.Id
    FROM wallet.Wallets AS w
    JOIN wallet.Users AS u ON u.Id = w.UserId
    WHERE u.Username = @ToUsername;

    IF @FromWalletId IS NULL OR @ToWalletId IS NULL
        THROW 50002, 'Wallet not found.', 1;
    IF @FromWalletId = @ToWalletId
        THROW 50004, 'You cannot send money to yourself.', 1;

    DECLARE @RequestHash BINARY(32) = HASHBYTES(
        'SHA2_256',
        CONCAT(CONVERT(VARCHAR(20), @ToWalletId), '|', CONVERT(VARCHAR(50), @Amount), '|IQD'));
    DECLARE @ExistingHash BINARY(32), @ExistingTransferId BIGINT, @ExistingExpiresAt DATETIME2;
    DECLARE @FirstId INT, @SecondId INT, @Ignore DECIMAL(18,2), @FromBalance DECIMAL(18,2);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- UPDLOCK + HOLDLOCK takes a serializable key-range lock on the composite PK.
        -- Simultaneous requests with this user/key therefore wait and observe one outcome.
        SELECT @ExistingHash = RequestHash,
               @ExistingTransferId = TransferId,
               @ExistingExpiresAt = ExpiresAt
        FROM wallet.IdempotencyKeys WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @FromUserId AND IdempotencyKey = @IdempotencyKey;

        IF @ExistingTransferId IS NOT NULL AND @ExistingExpiresAt > SYSUTCDATETIME()
        BEGIN
            IF @ExistingHash <> @RequestHash
                THROW 50003, 'Idempotency key was reused for a different request.', 1;

            SET @TransferId = @ExistingTransferId;
            SET @Replayed = 1;
            SELECT @NewBalance = FromBalanceAfter
            FROM wallet.Transfers
            WHERE Id = @ExistingTransferId;

            COMMIT TRANSACTION;
            RETURN;
        END;

        IF @ExistingTransferId IS NOT NULL
            DELETE FROM wallet.IdempotencyKeys
            WHERE UserId = @FromUserId AND IdempotencyKey = @IdempotencyKey;

        -- Lock both wallets in ascending id order to prevent opposite-direction deadlocks.
        SET @FirstId = IIF(@FromWalletId < @ToWalletId, @FromWalletId, @ToWalletId);
        SET @SecondId = IIF(@FromWalletId < @ToWalletId, @ToWalletId, @FromWalletId);
        SELECT @Ignore = Balance FROM wallet.Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @FirstId;
        SELECT @Ignore = Balance FROM wallet.Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @SecondId;
        SELECT @FromBalance = Balance FROM wallet.Wallets WHERE Id = @FromWalletId;

        IF @FromBalance < @Amount
            THROW 50001, 'Insufficient funds.', 1;

        UPDATE wallet.Wallets
        SET Balance = Balance - @Amount, LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = @CreatedBy
        WHERE Id = @FromWalletId;
        UPDATE wallet.Wallets
        SET Balance = Balance + @Amount, LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = @CreatedBy
        WHERE Id = @ToWalletId;

        SET @NewBalance = @FromBalance - @Amount;
        INSERT INTO wallet.Transfers
            (FromWalletId, ToWalletId, Amount, FromBalanceAfter, Currency, CreatedAt, CreatedBy)
        VALUES
            (@FromWalletId, @ToWalletId, @Amount, @NewBalance, 'IQD', SYSUTCDATETIME(), @CreatedBy);

        SET @TransferId = SCOPE_IDENTITY();
        INSERT INTO wallet.IdempotencyKeys
            (UserId, IdempotencyKey, RequestHash, TransferId, CreatedAt, ExpiresAt)
        VALUES
            (@FromUserId, @IdempotencyKey, @RequestHash, @TransferId,
             SYSUTCDATETIME(), DATEADD(HOUR, 24, SYSUTCDATETIME()));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR ALTER PROCEDURE wallet.usp_Transfer
    @FromUserId INT,
    @ToUsername NVARCHAR(50),
    @Amount DECIMAL(18,2),
    @CreatedBy NVARCHAR(256),
    @TransferId BIGINT OUTPUT,
    @NewBalance DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @Amount IS NULL OR @Amount <= 0
        THROW 50003, 'Amount must be greater than zero.', 1;

    DECLARE @FromWalletId INT, @ToWalletId INT;
    SELECT @FromWalletId = Id FROM wallet.Wallets WHERE UserId = @FromUserId;
    SELECT @ToWalletId = w.Id FROM wallet.Wallets AS w
    JOIN wallet.Users AS u ON u.Id = w.UserId WHERE u.Username = @ToUsername;
    IF @FromWalletId IS NULL OR @ToWalletId IS NULL
        THROW 50002, 'Wallet not found.', 1;
    IF @FromWalletId = @ToWalletId
        THROW 50003, 'You cannot send money to yourself.', 1;

    DECLARE @OwnsTran BIT = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    IF @OwnsTran = 1 BEGIN TRANSACTION;
    DECLARE @FirstId INT = IIF(@FromWalletId < @ToWalletId, @FromWalletId, @ToWalletId);
    DECLARE @SecondId INT = IIF(@FromWalletId < @ToWalletId, @ToWalletId, @FromWalletId);
    DECLARE @Ignore DECIMAL(18,2), @FromBalance DECIMAL(18,2);
    SELECT @Ignore = Balance FROM wallet.Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @FirstId;
    SELECT @Ignore = Balance FROM wallet.Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = @SecondId;
    SELECT @FromBalance = Balance FROM wallet.Wallets WHERE Id = @FromWalletId;
    IF @FromBalance < @Amount THROW 50001, 'Insufficient funds.', 1;

    UPDATE wallet.Wallets SET Balance = Balance - @Amount, LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = @CreatedBy
    WHERE Id = @FromWalletId;
    UPDATE wallet.Wallets SET Balance = Balance + @Amount, LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = @CreatedBy
    WHERE Id = @ToWalletId;
    INSERT INTO wallet.Transfers (FromWalletId, ToWalletId, Amount, Currency, CreatedAt, CreatedBy)
    VALUES (@FromWalletId, @ToWalletId, @Amount, 'IQD', SYSUTCDATETIME(), @CreatedBy);
    SET @TransferId = SCOPE_IDENTITY();
    SET @NewBalance = @FromBalance - @Amount;
    IF @OwnsTran = 1 COMMIT TRANSACTION;
END
""");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "wallet");

            migrationBuilder.DropColumn(
                name: "FromBalanceAfter",
                schema: "wallet",
                table: "Transfers");
        }
    }
}
