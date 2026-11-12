# ADR-0004: Kafka topic, partition key, and what "delivered" means

- **Status:** Proposed
- **Date:** 2026-11-12
- **Deciders:** Owner A, Owner B

## Context

Events flow from the Wallet API (outbox publisher) through Kafka to the Activity Service (consumer). Kafka gives at-least-once delivery, so every component has to tolerate duplicates. Transfers must keep working when Kafka is down (failure scenario 4). The consumer must catch up after downtime (scenario 6), must not double-count (scenario 5), and must survive a poison message (scenario 7). The read model must be rebuildable from the topic.

## Decision

### Topics

| Topic | Partitions | Retention | Purpose |
|---|---:|---|---|
| `paylite.transfers` | 3 | Forever (`retention.ms=-1`) | `TransferCompleted` events. Kept forever so `ActivityDb` can be rebuilt by replaying from offset 0. |
| `paylite.transfers.dlt` | 3 | 14 days | Messages the consumer gave up on, for inspection and manual replay. |

Replication factor 1, because compose runs a single broker. `kafka-init` creates both topics, and `Shared.Contracts/Kafka/Topics.cs` holds the names.

### Partition key: `toWalletId`

The message key is the recipient's wallet id, as a string. Everything the Activity Service builds is per recipient: the recipient's notifications and the merchant's daily summary. Keying by `toWalletId` puts all events for one recipient on one partition, in order, so exactly one consumer instance updates a given merchant's summary row. That means no write contention between consumer instances on the same row.

The sender side gets no ordering guarantee. Nobody consumes per-sender today. If someone does later, they can add a second event or key.

### Out of order

The summary is built only from commutative increments (`count += 1`, `total += amount`, `byHour[h] += amount`), bucketed by the event's `occurredAt` (UTC, converted to `Asia/Baghdad` for the day and hour). Applying the same set of events in any order gives the same result. Out of order is harmless as long as there are no duplicates, and duplicates are handled below. Notifications are listed sorted by `occurredAt`, not by arrival order.

### Producer (Owner A): "delivered" means the broker acknowledged the write

- The outbox row is inserted inside `wallet.usp_Transfer`, in the same transaction as the money movement. This is chosen over "an EF transaction around the procedure plus an EF insert" because the procedure is then one atomic unit no matter who calls it, it needs no extra round trip, and nobody can accidentally call it without writing the outbox row.
- The `eventId` (`NEWID()`) and the full envelope JSON (including `correlationId`) are created when the row is inserted, so every retry republishes the same `eventId`.
- An `OutboxPublisher` `BackgroundService` polls every second for a batch of up to 100 rows: `SELECT TOP (100) ... WITH (UPDLOCK, READPAST, ROWLOCK) WHERE SentAt IS NULL ORDER BY Id`. `READPAST` lets a second instance skip rows that are already being published instead of sending them twice.
- Producer config: `Acks = All`, `EnableIdempotence = true`. A row gets `SentAt = SYSUTCDATETIME()` only after the delivery report says `Persisted`.
- If Kafka is down, the publish fails, the row stays unsent, and the publisher backs off (1s up to 30s) and tries again. Transfers never wait for Kafka.
- If the process crashes after the broker acknowledged the message but before `SentAt` is set, the event is published again. That's the at-least-once duplicate the consumer is built to absorb.

### Consumer (Owner B): "delivered" means the effect is committed in `ActivityDb`, then the offset is committed

- Consumer group `activity-service`, `EnableAutoCommit = false`, `AutoOffsetReset = Earliest`, so a brand-new group (or a rebuild) starts from the beginning.
- For each message, in one `ActivityDb` transaction: insert into `activity.ProcessedEvents(EventId PK)`, insert the notification, and upsert the merchant's daily summary. A duplicate fails the first insert with `2627`: roll back, log "already processed", and treat it as success.
- Commit the offset only after that transaction commits (or after the duplicate is skipped, or after the message is safely on the DLT). A crash before the offset commit just redelivers the message, and the `ProcessedEvents` check absorbs it.
- **Failures:**
  - *Poison* (invalid JSON, missing fields, a `version` we don't know, an amount <= 0): straight to the DLT, with no retries, because retrying cannot fix it.
  - *Transient* (SQL timeout, deadlock 1205, connection lost): retry in place 3 times with backoff (1s, 2s, 4s), then send to the DLT. The partition is blocked for at most about 7 seconds, never forever.
  - Unknown `eventType`: log it and skip it (commit). That keeps us forward-compatible with event types we do not consume.
- **DLT message:** the original key and value bytes, unchanged, plus the headers `dlt-reason`, `dlt-error`, `dlt-original-topic`, `dlt-original-partition`, `dlt-original-offset`, and `dlt-failed-at`. If producing to the DLT itself fails, we do not commit the offset; we stop and retry, because nothing gets lost that way.
- The SignalR push happens after the commit and is best-effort. It is not part of "delivered". A user who misses it still sees the notification from `GET /api/notifications`.

### Rebuilding the read model

Stop the Activity Service, drop and re-migrate `ActivityDb`, reset the `activity-service` group's offsets to the earliest offset (`kafka-consumer-groups.sh --reset-offsets --to-earliest --execute`), and start the service again. Retention is forever, and the processing is idempotent and order-independent, so the result is identical.

## Consequences

- Neither side needs Kafka transactions or exactly-once semantics. Correctness comes from the outbox on one side and the `ProcessedEvents` primary key on the other: at-least-once delivery plus idempotent effects gives exactly-once results.
- Kafka down never blocks money. Events arrive late, but they arrive.
- At most 3 consumer instances do useful work (one per partition). That's plenty for the demo, and we can raise the partition count later at the cost of re-keying.
- `ProcessedEvents` grows forever, by one small row per event. That's fine at our scale, and it's what makes replay safe.
- A transient error longer than about 7 seconds sends good events to the DLT. Someone then has to replay them from the DLT. An Admin page for that is a stretch goal.
