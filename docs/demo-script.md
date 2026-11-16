# Demo Day script (Tue 2026-11-17, 45 minutes)

Order agreed in `TeamRequests/2026-11-15-A-to-B-demo-day-plan.md`. **Salih** presents the Wallet side and
scenarios 1–4; **Aboalhassan** presents the Activity side and scenarios 5–7. Commands for every drill are
in [`runbook.md`](runbook.md).

## Before the audience arrives (15 min)

1. `docker compose down -v && docker compose up --build`: start from nothing, like a fresh clone.
2. `curl localhost:8081/health` and `curl localhost:8082/health`: both `Healthy`.
3. Three terminals side by side:
   - `docker compose logs -f wallet-api activity-service`
   - one for drill commands
   - consumer lag (`kafka-consumer-groups.sh --describe --group activity-service`)
4. Two browser windows: **left** = the customer (normal window), **right** = the merchant (private window).
5. Register `ali` (User) and `coffeeshop` (Merchant) **now**, so the 10-minute happy path doesn't wait for typing.

## 1. What PayLite is (5 min, Salih)

- A small wallet: top up, send money, pay a merchant; the recipient gets a live notification.
- Architecture diagram ([`architecture.md`](architecture.md)): two services, each with its own database,
  talking **only** through Kafka. The Activity Service never reads `WalletDb`.
- One sentence each: the outbox (Salih) and the idempotent consumer (Aboalhassan).

## 2. Happy path (10 min)

| Step | Who | Show |
|---|---|---|
| Top up `ali` with 100,000 | Salih | Balance updates; `formatMoney` everywhere. |
| `ali` sends 25,000 to `coffeeshop` | Salih | `201`; History shows it; Wallet log line with the correlation id. |
| Right window | Aboalhassan | **The bell goes to 1 without a refresh**; Notifications page shows it; Daily summary shows 1 payment / 25,000 in the current hour; Incoming payments lists it. |
| Follow one transfer | Aboalhassan | `docker compose logs wallet-api activity-service \| grep <id>`: the same id from the HTTP request to the Activity Service. |
| Mark as read | Aboalhassan | The bell goes back to 0. |

## 3. Failure demo (15 min)

| # | Who | Do | What the audience should see |
|---|---|---|---|
| 1 | Salih | Double-click **Send** | One transfer; the button is disabled while in flight; one notification on the right. |
| 4 | Salih + Aboalhassan | `docker compose stop kafka`, send 700, `docker compose start kafka` | `201` immediately and the balance moves; `/health` says Degraded; the outbox row has `SentAt = NULL`; ~10 s after Kafka is back: "Published event", then the **notification appears live**. |
| 6 | Aboalhassan | `docker compose stop activity-service`, send 3 transfers, show the lag, `docker compose start activity-service` | Lag 3 → 0; three "Processed event" lines within a second; 3 notifications, summary total = sum of the 3. |
| 5 | Aboalhassan | Re-publish an already-processed event (runbook drill 5) | `already processed, skipping`; the notification count and the summary don't change. Explain: the `ProcessedEvents` primary key + one transaction. |
| 7 | Aboalhassan | `echo 'this is not json' \| ... kafka-console-producer ...` | Log: `Sent ... to paylite.transfers.dlt (poison)`; read the DLT with headers; **send one more transfer: it still arrives**, so the consumer kept going. |

If something goes wrong live: say what you expected, show `/health` and the logs for the correlation id. That
**is** the debugging story.

## 4. One design decision each (5 min each)

**Salih: the transactional outbox, and the procedure doing the work.** The event row is written by
`usp_Transfer` in the same transaction as the money, so an event exists if and only if the money moved. The
trade-off: events are delayed by the poll interval (≤ 1 s) and can be published twice after a crash, which is
why the consumer has to be idempotent.

**Aboalhassan: idempotency from a primary key, not a check.** `ProcessedEvents.EventId` is the primary key,
and its insert shares one transaction with the notification and the summary update. A duplicate fails with
2627 and the whole thing rolls back. "Check first, then insert" has a gap where two consumers both see
"not processed". The trade-off: `ProcessedEvents` grows by one row per event forever, but that's also what
makes rebuilding `ActivityDb` by replaying the topic safe. Also: the offset is committed only after the
commit (or the DLT write), and transient errors get 3 retries before the DLT, so a partition is never blocked.

## 5. Questions (5 min)

Expect questions about **each other's** half. Short answers:

| Question | Answer |
|---|---|
| Why lock the two wallets lowest id first? | A→B and B→A at once would each hold one lock and wait for the other (deadlock, error 1205). A fixed order means one just waits. |
| What does `Idempotency-Key` protect against? | A double-click or a retry creating a second transfer. Same key + same request → the original result; same key + different request → 422. |
| Why is the key stored only for successful transfers? | A failed transfer moved no money, so retrying it with the same key after a top-up should work. |
| Why `toWalletId` as the Kafka key? | Everything the Activity Service builds is per recipient, so one recipient's events stay in order on one partition. |
| Why `Clients.User` and not a group? | The JWT's user id is the SignalR user id, so a push goes to that user's connections only. |
| Why is the token in the URL for SignalR? | Browsers can't set headers on a WebSocket. We accept `?access_token=` only on `/hubs/notifications`, and nginx doesn't log that path. |
| Why keep the token in memory, not `localStorage`? | An XSS script can read `localStorage`. The trade-off: a refresh logs you out. |
| What if `ActivityDb` is wiped? | Reset the consumer group to the earliest offset and replay: the topic keeps events forever, and processing is idempotent and order-independent. |
