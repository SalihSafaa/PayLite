# Runbook: scenario 4 drill, tested in compose

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

I ran the full flow in compose from a fresh `docker compose up` (APIs connecting as `wallet_app` /
`activity_app`), with your consumer from #10. Transfer → outbox → Kafka → consumer → notification row
works, and scenario 4 passes. The runbook is yours, so here are the exact steps and what we should see.
Can you put them in `docs/runbook.md` (the "Logs for one transfer" TODO and the drill 4 row)?

## Logs for one transfer

```bash
docker compose logs wallet-api activity-service | grep <correlation-id>
```

## Drill 4: Kafka down during a transfer

```bash
docker compose stop kafka
curl http://localhost:8081/health          # "Degraded", kafka: "Kafka is unreachable."
# make a transfer in the UI (or curl with an X-Correlation-ID header)
#   -> 201 straight away (16-130 ms in my runs); money has moved
docker compose start kafka
docker compose logs -f wallet-api          # wait for "Published event ..."
```

What the Wallet log shows (after the log clean-up in this PR), all with the same correlation id:

```
[13:07:16 INF] [demo-kafka-down-004] Transfer 4: user 2 sent 700 to coffeeshop
[13:07:16 INF] [demo-kafka-down-004] HTTP POST /api/transfers responded 201 in 80.2264 ms
[13:07:26 WRN] [demo-kafka-down-004] Could not publish event 9929...: Local: Message timed out. Will retry.
[13:07:35 INF] [demo-kafka-down-004] Published event 9929... (TransferCompleted) to paylite.transfers [1] @ 3
```

and your service, within a second of the publish:

```
[13:07:36 INF] Processed event 9929...: transfer 4, 700.00 IQD to user 3 (correlation demo-kafka-down-004)
```

The event goes out about 6-10 s after `docker compose start kafka`. To show the waiting row on screen:

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C \
  -Q "SELECT Id, CorrelationId, SentAt, Attempts, LastError FROM WalletDb.wallet.OutboxMessages"
```

`SentAt` is NULL while Kafka is down and gets filled in when it's back.

## Answer

From: Aboalhassan (B) · 2026-11-16

Done in #19: `docs/runbook.md` now has "Logs for one transfer" (with your log lines as the example) and drill 4 with your steps and the outbox query, plus drills 1, 5, 6 and 7, rebuilding the read model, and common problems.

One small change: the `sqlcmd` lines run inside the container as `bash -c '... -P "$MSSQL_SA_PASSWORD" ...'`, so they work even when `DB_PASSWORD` isn't exported in the presenter's shell.
