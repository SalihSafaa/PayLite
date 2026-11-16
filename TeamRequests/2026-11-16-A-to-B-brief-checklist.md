# Brief checklist before Demo Day: reviews, ADRs, drill 5

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

Thanks for #16–#19. I merged all of them with `main` and my #20 locally and tested them: all tests pass
(41 Wallet + 37 Activity + 33 frontend), and in a fresh `docker compose up` the happy path works (the
merchant's bell shows the payment about 1 second after the transfer, without a refresh, and the summary
updates live) and scenario 7 lands on the DLT with every `dlt-*` header. Details are in my reviews on your PRs.

Three things left from the brief:

## 1. Drill 5 command in the runbook fails (#19)

`sqlcmd ... -Q "UPDATE WalletDb.wallet.OutboxMessages SET SentAt = NULL ..."` fails with
`Msg 1934 ... incorrect settings: 'QUOTED_IDENTIFIER'` (and exits 0). sqlcmd runs with QUOTED_IDENTIFIER OFF,
and SQL Server refuses an UPDATE on a table with a filtered index (`IX_OutboxMessages_Unsent`). Adding `-I`
fixes it; I tested it. Scenario 5 is one of the three the brief requires live.

`scripts/publish-sample-event.sh replay` (in #20) does the same UPDATE with `-I -b`, so the runbook could
also just call that.

## 2. Real review comments (both of us)

The brief: "A review means reading the code and leaving real comments. Clicking Approve isn't a review",
and reviews are part of the individual grade. All approvals so far (mine and yours) are "no comment".
I'm leaving real comments on #16–#19 today; it would help both of us if you did the same on my PRs
(#20 now, and #11–#15 if you have time).

## 3. ADRs: Proposed → Accepted

ADR-0001, 0003 and 0004 still say `Status: Proposed` on `main` and on all your branches. We follow all of
them, and the team grade looks at "contracts and ADRs: clear, agreed, and actually followed". Can you set
them to `Accepted` in #19?

## The script you asked for (#20)

```bash
scripts/publish-sample-event.sh replay                     # scenario 5: the outbox publishes the newest sent event again
scripts/publish-sample-event.sh poison                     # scenario 7: invalid JSON on the topic
scripts/publish-sample-event.sh sample 3 3 1234 merchant   # a new valid event (toUserId toWalletId amount), no money moves
```

`replay` uses your drill 5 approach (clear `SentAt`, the real publisher re-sends it), so it behaves exactly
like the publisher crash in ADR-0004.

## Answer

From: Aboalhassan (B) · 2026-11-16

Thanks for testing all of it end to end.

1. **Drill 5:** fixed in #19. Every `sqlcmd` line in the runbook now has `-I -b`, and drills 5 and 7 call your
   script (`replay` / `poison`), so the runbook and the demo script use the same tested commands. The runbook
   also explains the filtered-index reason, in case the mentor asks.
2. **Reviews:** agreed. I'm answering your comments on #16 and #17 in the threads, and I'll leave real comments
   on #20 and on #11–#15 today.
3. **ADRs:** 0001, 0003 and 0004 are `Accepted` in #19 (0002 already was).

Your #16 comments are handled in #21 (`fix/activity-review-followups`): correlation ids are checked with your
regex before they're logged, non-IQD events are poison, and EF's `[ERR]` line on an expected duplicate is gone.
For the "ActivityDb down for 5 minutes" question I went with (a) for now: a "Replaying the dead-letter topic"
section in the runbook. Pausing the consumer while the DB health check fails is the better long-term answer;
I'd rather not change the consumer loop the day before the demo.

