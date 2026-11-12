# ADR-0002: How transfer idempotency works

- **Status:** Proposed
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
| `RequestHash` | `binary(32)` | SHA-256 of the canonical request: `toWalletId|amount|currency`, with the amount formatted in invariant culture. |
| `TransferId` | `bigint` | The transfer this key created. |
| `CreatedAt`, `ExpiresAt` | `datetime2` | `ExpiresAt = CreatedAt + 24h`. |

Primary key: `(UserId, IdempotencyKey)`. A key from user X can never collide with, or reveal, a transfer made by user Y.

**The key is claimed inside `wallet.usp_Transfer`, in the same transaction as the money movement.** The procedure takes `@UserId, @IdempotencyKey, @RequestHash` and does the following:

1. Insert the key row first. Two concurrent requests with the same key now race on the primary key, not on a `SELECT`. The second one blocks on the first one's key lock until that transaction commits or rolls back.
2. If the insert fails with a duplicate key (`2627`), the procedure reads the existing row. If the hash is different, it does `THROW 50003` and the API returns `422`. If the hash is the same, it returns the existing `TransferId` and the API returns the original transfer (`201`, same body, plus the header `Idempotent-Replayed: true`).
3. Otherwise it locks both wallets (lowest id first), moves the money, inserts the transfer and the outbox row (ADR-0004), sets `TransferId` on the key row, and commits.

**Only successful transfers are remembered.** If the transfer fails (insufficient funds `50001`, wallet not found `50002`), `XACT_ABORT` rolls back the key row together with everything else. A retry with the same key therefore runs again. That's safe, because no money moved the first time. It also means a user who tops up after an "insufficient funds" error can press Retry without being stuck on an old 409.

**Validation.** A missing `Idempotency-Key` header, or one that is not a UUID, returns `400`.

**Retention: 24 hours.** A retry or double-click happens within seconds or minutes, and 24 hours covers a browser tab left open overnight. A `BackgroundService` in the Wallet API deletes expired rows once an hour (`DELETE TOP (1000) ... WHERE ExpiresAt < SYSUTCDATETIME()`, in a loop). The transfer itself is kept forever. Only the dedup record expires.

**Frontend.** `SendMoneyPage` creates one `crypto.randomUUID()` when the form is filled in, reuses it for every retry of that same submission, and creates a new one only after a success or when the user changes the form. The submit button is disabled while the request is in flight.

## Consequences

- Exactly-once transfers come from the database constraint, not from timing. The concurrent duplicate case is covered by the same mechanism as a sequential duplicate.
- One round trip and one transaction: the key, the transfer, and the outbox row are either all there or none of them are.
- A concurrent duplicate waits briefly on a lock instead of failing. The wait is short, because the transfer transaction is short.
- After 24 hours the same key would create a new transfer. We accept that: no real client retries a day later.
- The request hash has to be computed in one place (the transfer service), so the UI and the API can never disagree about what "the same request" means.
