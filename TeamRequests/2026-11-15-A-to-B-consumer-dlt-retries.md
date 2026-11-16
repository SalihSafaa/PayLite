# Consumer: dead-letter topic and retry limit

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

Reviewed your consumer PR (#10, `DevBDay5`): merged with current `main` it builds and the tests
pass, nice work on the transaction + 2627 handling and the summary upsert. Two things ADR-0004
asks for that I couldn't find:

1. **Poison messages go to `paylite.transfers.dlt`** (with the `dlt-reason`, `dlt-error`,
   `dlt-original-*` headers), not just logged and skipped. Scenario 7 is demoed live.
2. **Transient failures: 3 retries (1s, 2s, 4s), then DLT.** Right now a SQL outage retries the
   same message every 2s forever and blocks its partition.

Demo Day is 2026-11-17, so these need to land by tomorrow. Are they coming in #10 or a follow-up
PR? Once both are in, let's run the full flow in compose together: transfer → outbox → Kafka →
your consumer → notification (and scenario 4 with Kafka stopped).

## Answer

From: Aboalhassan (B) · 2026-11-16

Both in a follow-up PR, #16 (`feat/activity-dead-letter`), CI green (Activity tests 21/21):

1. **Poison → `paylite.transfers.dlt` at once**, with the original key, value and headers plus `dlt-reason`, `dlt-error`, `dlt-original-topic`, `dlt-original-partition`, `dlt-original-offset`, `dlt-failed-at` and `dlt-attempts`. The consumer reads raw bytes now, so a value that isn't even UTF-8 also goes to the DLT instead of blocking the partition inside the Kafka client.
2. **Transient → 3 retries (1 s, 2 s, 4 s) → DLT** (`dlt-reason: retries-exhausted`), each attempt in a new DI scope. The offset is committed only after the effect is committed, the duplicate is skipped, or the DLT write is acknowledged; if the DLT write fails, nothing is committed and the message is tried again.

Tests: `DeadLetterIntegrationTests` (3) and `RetryIntegrationTests` (2). Yes to the joint compose run, see the demo plan answer.
