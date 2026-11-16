# README: Wallet rows of the failure table, and concurrency notes

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

The README is yours, so here is the text for my part. Everything below is on `main` and green in CI.

## 1. Failure scenarios table: replace the `_TODO_` in rows 1-4

| # | Test |
|---|---|
| 1 | `IdempotencyIntegrationTests.Transfer_WithSameKeyAndSameRequest_ReturnsOriginalResultAndReplayHeader`, `Transfer_ParallelDuplicatesWithSameKey_OnlyOneCreatesTransfer` |
| 2 | `TransferIntegrationTests.Transfer_20InParallel_ExactlyTenSucceedAndBalanceNeverNegative` |
| 3 | `TransferIntegrationTests.Transfer_BothDirectionsAtOnce_AllSucceedWithoutDeadlock` |
| 4 | `OutboxIntegrationTests.KafkaDown_TransferSucceedsImmediately_EventIsSentWhenKafkaReturns` (Wallet side) + manual drill in compose (see `2026-11-15-A-to-B-runbook-scenario-4.md`) |

Rows 5-7 are yours.

## 2. Concurrency section

Transfers return **201** now (since #11), so please change "40 × 200" to "40 × 201" and "10 × 200" to
"10 × 201". Then add these two paragraphs after the scenario 2 one:

```markdown
**Same transfer twice (scenario 1).** The key lookup in `wallet.usp_Transfer` uses
`UPDLOCK, HOLDLOCK`, so two requests with the same `Idempotency-Key` can't both miss it: the second
waits until the first commits, then finds the key and gets the stored result back (201 with
`Idempotent-Replayed: true`), with no second transfer and no second event. Test: 10 parallel requests with
one key → 10 × 201, exactly one transfer, balance down by 1,000 once. `HOLDLOCK` locks only that key's
range, so different users don't wait for each other (300 concurrent transfers between different users
all succeeded when we measured it on Day 3). A failed transfer (e.g. 409) stores no key, so the same
key can be retried after a top-up.

**Outbox publisher.** It takes up to 100 unsent rows with `UPDLOCK, READPAST`: a second publisher
instance would skip rows that are already locked instead of sending them twice. `SentAt` is set only
after Kafka confirms the write (`Acks=All`, idempotent producer). If Kafka is down, the publisher stops
the batch and backs off (1 s up to 30 s), so events go out in order once it's back.
```

Can you get these in before the demo?

## Answer

From: Aboalhassan (B) · 2026-11-16

Done in #19 (this PR): rows 1–4 with your test names (plus `SendMoneyPage.test.tsx` for the double-click), rows 5–7 with mine, "40 × 201" / "10 × 201", and your two paragraphs after scenario 2. I added one paragraph for the consumer side (scenarios 5–7) so the section covers both services. I also updated the README's status line, which still said "skeleton".
