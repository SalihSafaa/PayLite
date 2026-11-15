# Daily standups

One line each per day: what I did · what I'm doing · what's blocking me.

| Day | Date | Salih (Owner A) | Aboalhassan (Owner B) |
|---|---|---|---|
| 1 | 2026-11-11 | Read the brief, merged the repo skeleton, fixed the NuGet cache in the Dockerfiles · wallets + top-ups (PR #1) · nothing | Read the brief and the skeleton, reviewed what Salih built · drafting ADRs 0001–0004 · nothing |
| 2 | 2026-11-12 | Merged wallets + top-ups (PR #1), started atomic transfers on `DevADay2` · transfers + idempotency · nothing | Wrote ADRs 0001–0004 (PR #2), split the shared work, picked Mantine, set the Kafka topic retention · Activity Service schema (ProcessedEvents, Notifications, MerchantDailySummaries) · PRs #1 and #2 were merged without a review, so we're enabling branch protection on `main` |
| 3 | 2026-11-15 | Idempotent transfers (#6), outbox + correlation ID (#9), history + merchant payments endpoints (#11, #12), Send money / History / dashboard pages (#13); ran the full flow and drills 4, 6 and 7 in compose, cleaned up the logs (#14), first frontend tests · Demo Day rehearsal with Aboalhassan · scenario 7 (dead-letter topic) isn't in yet, and it's demoed live | Day 1–2 cleanup merged (#5); event contract change + Activity tables (`DevBDay4`) · event processor + Kafka consumer (`DevBDay5`) · need Salih's approval on the contract change |
