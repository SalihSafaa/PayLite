# Consumer: dead-letter topic and retry limit

Status: Open
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
