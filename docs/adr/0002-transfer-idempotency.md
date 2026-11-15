# ADR-0002: How transfer idempotency works

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** Owner A, Owner B

## Context

The frontend sends an `Idempotency-Key` (UUID) with every `POST /api/transfers`. The same key with the same request must return the original result and must not create a second transfer. The same key with a different request must return `422`. The hard case is two requests with the same key arriving at the same moment (a double-click, or a retry while the first request is still running). A plain "check, then insert" lets both of them through.

## Decision

**Storage.** Keys go in a table `wallet.IdempotencyKeys`:

| Column | Type | Notes |
|---|---|---|
| `UserId` | `int` | Part of the primary key: a key is scoped to the user who sent it. |
| `IdempotencyKey` | `uniqueidentifier` | Part of the primary key. |
| `RequestHash` | `binary(32)` | SHA-256 of the canonical request: `toWalletId|amount|currency`, computed by SQL after resolving the recipient wallet. |
| `TransferId` | `bigint` | The transfer this key created. |
| `CreatedAt`, `ExpiresAt` | `datetime2` | `ExpiresAt = CreatedAt + 24h`. |

Primary key: `(UserId, IdempotencyKey)`. A key from user X can never collide with, or reveal, a transfer made by user Y.

**Idempotency is enforced inside `wallet.usp_Transfer`, in the same transaction as the money movement.** The procedure takes the sender user id, recipient username, amount, actor, and `@IdempotencyKey`; it resolves the recipient wallet and computes the request hash itself. It does the following:

1. In a transaction, select the `(UserId, IdempotencyKey)` row with `UPDLOCK, HOLDLOCK`. The composite primary key provides a range lock, so concurrent requests using the same key serialize, including when no row exists yet.
2. If an unexpired row exists with a different hash, the procedure throws `50003` and the API returns `422`. If the hash matches, it returns the stored transfer id and `FromBalanceAfter`; the API returns the original body with status `201` and `Idempotent-Replayed: true`.
3. If there is no active key row, lock both wallets (lowest id first), move the money, store the transfer including the sender's balance after the transfer, insert the idempotency row with a 24-hour expiry, and commit. Expired key rows are replaced when used again.

**Only successful transfers are remembered.** If the transfer fails (insufficient funds `50001`, wallet not found `50002`, invalid transfer `50004`), the transaction rolls back without an idempotency record. A retry with the same key therefore runs again. That allows a user to top up after an insufficient-funds response and retry without being stuck on an old 409.

**Error mapping.** `50001` maps to `409 Conflict`, `50002` to `404 Not Found`, `50003` (same key, different request) to `422 Unprocessable Entity`, and `50004` (invalid amount or self-transfer) to `400 Bad Request`.

**Validation.** A missing `Idempotency-Key` header, or one that is not a UUID, returns `400`.

**Retention: 24 hours.** A retry or double-click happens within seconds or minutes, and 24 hours covers a browser tab left open overnight. A `BackgroundService` in the Wallet API deletes expired rows once an hour (`DELETE TOP (1000) ... WHERE ExpiresAt < SYSUTCDATETIME()`, in a loop). The transfer itself is kept forever. Only the dedup record expires.

**Frontend.** `SendMoneyPage` creates one `crypto.randomUUID()` when the form is filled in, reuses it for every retry of that same submission, and creates a new one only after a success or when the user changes the form. The submit button is disabled while the request is in flight.

## Consequences

- At-most-once transfer execution per user/key comes from the database lock and transaction, not application timing. Sequential and concurrent duplicates use the same path.
- The transfer and deduplication row commit together or both roll back.
- A concurrent duplicate waits briefly on a lock instead of failing. The wait is short, because the transfer transaction is short.
- After 24 hours the same key would create a new transfer. We accept that: no real client retries a day later.
- The request hash is computed in one place (the stored procedure), so the client and API cannot disagree about what "the same request" means.
