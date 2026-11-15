# Daily standups

One line each per day: what I did · what I'm doing · what's blocking me.

| Day | Date | Salih (Owner A) | Aboalhassan (Owner B) |
|---|---|---|---|
| 1 | 2026-11-11 | Read the brief, merged the repo skeleton, fixed the NuGet cache in the Dockerfiles · wallets + top-ups (PR #1) · nothing | Read the brief and the skeleton, reviewed what Salih built · drafting ADRs 0001–0004 · nothing |
| 2 | 2026-11-12 | Merged wallets + top-ups (PR #1), started atomic transfers on `DevADay2` · transfers + idempotency · nothing | Wrote ADRs 0001–0004 (PR #2), split the shared work, picked Mantine, set the Kafka topic retention · Activity Service schema (ProcessedEvents, Notifications, MerchantDailySummaries) · PRs #1 and #2 were merged without a review, so we're enabling branch protection on `main` |
| 3 | 2026-11-15 | _(Salih fills in)_ | Event processor + Kafka consumer, scenario 5 test (#10); answered Salih's team requests; asked Salih to turn on branch protection; first end-to-end run in compose blocked on #10 review · retries + dead-letter topic, scenario 7 (`feat/activity-dead-letter`) · waiting for Salih's review on #10 |
