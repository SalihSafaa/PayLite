using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.WalletApi.Migrations
{
    /// <inheritdoc />
    public partial class AddOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "wallet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventId",
                schema: "wallet",
                table: "OutboxMessages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Unsent",
                schema: "wallet",
                table: "OutboxMessages",
                column: "Id",
                filter: "[SentAt] IS NULL");

            migrationBuilder.Sql("""
CREATE OR ALTER PROCEDURE wallet.usp_Transfer
    @FromUserId      INT,
    @ToUsername      NVARCHAR(50),
    @Amount          DECIMAL(18,2),
    @CreatedBy       NVARCHAR(256),
    @IdempotencyKey  UNIQUEIDENTIFIER,
    @CorrelationId   NVARCHAR(64),
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

    -- Both wallets and their owners. The owners go into the event: the Activity Service
    -- never reads WalletDb, so the event must say who to notify.
    DECLARE @FromWalletId INT, @ToWalletId INT, @ToUserId INT,
            @FromUsername NVARCHAR(50), @ToIsMerchant BIT;
    SELECT @FromWalletId = w.Id, @FromUsername = u.Username
    FROM wallet.Wallets AS w
    JOIN wallet.Users AS u ON u.Id = w.UserId
    WHERE w.UserId = @FromUserId;
    SELECT @ToWalletId = w.Id, @ToUserId = u.Id,
           @ToIsMerchant = IIF(u.Role = 'Merchant', 1, 0)
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
        DECLARE @Now DATETIME2 = SYSUTCDATETIME();
        INSERT INTO wallet.Transfers
            (FromWalletId, ToWalletId, Amount, FromBalanceAfter, Currency, CreatedAt, CreatedBy)
        VALUES
            (@FromWalletId, @ToWalletId, @Amount, @NewBalance, 'IQD', @Now, @CreatedBy);

        SET @TransferId = SCOPE_IDENTITY();
        INSERT INTO wallet.IdempotencyKeys
            (UserId, IdempotencyKey, RequestHash, TransferId, CreatedAt, ExpiresAt)
        VALUES
            (@FromUserId, @IdempotencyKey, @RequestHash, @TransferId,
             SYSUTCDATETIME(), DATEADD(HOUR, 24, SYSUTCDATETIME()));

        -- TRANSACTIONAL OUTBOX (ADR-0004): the event is written in the SAME transaction as the
        -- money, so it exists if and only if the transfer committed. OutboxPublisher sends it to
        -- Kafka later, so a transfer never waits for Kafka. The eventId is fixed here, so every
        -- retry of the publish sends the same id and the consumer can drop duplicates.
        DECLARE @EventId UNIQUEIDENTIFIER = NEWID();
        SET @CorrelationId = ISNULL(NULLIF(@CorrelationId, ''), CONVERT(NVARCHAR(64), NEWID()));

        INSERT INTO wallet.OutboxMessages
            (EventId, EventType, Topic, MessageKey, Payload, CorrelationId, CreatedAt, Attempts)
        VALUES (
            @EventId,
            'TransferCompleted',
            'paylite.transfers',
            CONVERT(NVARCHAR(50), @ToWalletId),   -- partition key: the recipient's wallet (ADR-0004)
            JSON_OBJECT(
                'eventId': @EventId,
                'eventType': 'TransferCompleted',
                'version': 1,
                'occurredAt': CONVERT(VARCHAR(33), @Now, 127) + 'Z',
                'correlationId': @CorrelationId,
                'data': JSON_OBJECT(
                    'transferId': @TransferId,
                    'fromWalletId': @FromWalletId,
                    'fromUserId': @FromUserId,
                    'fromUsername': @FromUsername,
                    'toWalletId': @ToWalletId,
                    'toUserId': @ToUserId,
                    'toIsMerchant': @ToIsMerchant,
                    'amount': @Amount,
                    'currency': 'IQD')),
            @CorrelationId,
            @Now,
            0);

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
            // Put back the Day 3 version of the procedure (no outbox, no correlation id).
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

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "wallet");
        }
    }
}
