# Brief checklist before Demo Day: reviews, ADRs, live demo

Status: Open
From: Salih (A) · To: Aboalhassan (B)

I went through the brief again against what's on `main`. Three things count towards our grades that
we haven't done yet, plus the script you asked for.

## 1. Real review comments (both of us)

The brief: "A review means reading the code and leaving real comments. Clicking Approve isn't a
review", and our reviews are part of the individual grade. All approvals so far (mine and yours) are
"no comment". From now on let's each leave real comments (questions, bugs, suggestions) on every PR
before approving. I'm adding mine to #10 now; it would help both of us if you did the same on
#11–#15.

## 2. ADRs: Proposed → Accepted

ADR-0001, 0003 and 0004 still say `Status: Proposed`. We follow all of them, and the team grade looks
at "contracts and ADRs: clear, agreed, and actually followed". The ADRs are yours: can you set them to
`Accepted` in your next PR? (ADR-0001's follow-up, top-up minimum 1 and whole dinars, is done.)

## 3. The live demo, per the brief's agenda

- **Happy path (10 min):** "the notification appears live and the merchant summary updates". That needs
  the SignalR hub, the bell and the daily summary endpoint and page. Will they be ready by Tuesday?
  If not, what do we show instead (e.g. the notifications list after a refresh)?
- **Failure demo (15 min), at least 1, 4 and 5:** I'll do 1 (double-click) and 4 (stop Kafka). For 5
  you can now use the script below.
- **One design decision each, with its trade-off (5 min each):** mine is lock ordering in
  `usp_Transfer`. Which one is yours?

## 4. The sample-event script you asked for (in my PR)

`scripts/publish-sample-event.sh`, tested in compose:

```bash
scripts/publish-sample-event.sh replay        # scenario 5: re-sends the newest real event from the outbox
scripts/publish-sample-event.sh poison        # scenario 7: puts invalid JSON on the topic
scripts/publish-sample-event.sh sample 3 3 1234 merchant   # a new valid event (toUserId toWalletId amount)
```

What I saw: `replay` → your service logs "already processed, skipping" and the counts don't change ✅.
`sample` → a notification and a summary row, no money moves ✅. `poison` → logged and skipped; the DLT
part is your `feat/activity-dead-letter` work.

One thing for the scenario 5 demo: on a duplicate, EF Core also logs
`[ERR] An exception occurred in the database while saving changes ...` before your "already processed"
line. It's the expected path, but on screen it looks like a failure. Maybe insert into
`ProcessedEvents` first with plain SQL and catch 2627 there, or lower EF's `SaveChangesFailed` log level.
