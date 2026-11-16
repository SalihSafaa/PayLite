# Runbook

How to run PayLite, check it's healthy, follow one transfer through the logs, and run the failure drills
from Demo Day. Every command runs from the repository root.

## Run it

```bash
cp .env.example .env        # first time only; fill in the passwords and JWT_KEY
docker compose up --build
```

| URL | What |
|---|---|
| http://localhost:3000 | Frontend |
| http://localhost:8081/swagger | Wallet API Swagger |
| http://localhost:8082/swagger | Activity Service Swagger |

Start over from nothing (deletes both databases and Kafka's data): `docker compose down -v`.

## Is everything up?

```bash
docker compose ps
curl http://localhost:8081/health
curl http://localhost:8082/health
```

Both health endpoints report SQL Server and Kafka separately. Kafka down shows as `Degraded` (not
`Unhealthy`): the services keep serving requests, and transfers keep working.

## Logs for one transfer

Every log line starts with `[<correlation id>]`. The frontend sends a new `X-Correlation-ID` with each
request, the Wallet API logs it, puts it in the outbox row and the Kafka event, and the Activity Service
logs it again. One `grep` shows the whole journey:

```bash
docker compose logs wallet-api activity-service | grep <correlation-id>
```

What one transfer to a merchant looks like:

```
wallet-api        | [13:07:16 INF] [demo-001] Transfer 4: user 2 sent 700 to coffeeshop
wallet-api        | [13:07:16 INF] [demo-001] HTTP POST /api/transfers responded 201 in 80.2264 ms
wallet-api        | [13:07:17 INF] [demo-001] Published event 9929... (TransferCompleted) to paylite.transfers [1] @ 3
activity-service  | [13:07:17 INF] [demo-001] Processed event 9929...: transfer 4, 700.00 IQD to user 3 (correlation demo-001)
```

The id is also shown in the UI under any error (`Reference: ...`), so a user's screenshot is enough to find
the logs.

## Kafka

```bash
# list topics
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
# read the dead-letter topic from the start, with its dlt-* headers
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 \
  --topic paylite.transfers.dlt --from-beginning --property print.headers=true --property print.key=true
# consumer group lag (LAG = events waiting for the Activity Service)
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 \
  --describe --group activity-service
```

## Looking at the databases

```bash
# Wallet: outbox rows (SentAt is NULL while Kafka is down)
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "SELECT Id, CorrelationId, SentAt, Attempts, LastError FROM WalletDb.wallet.OutboxMessages"'

# Activity: notifications and the merchant summary
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "SELECT UserId, Amount, FromUsername, OccurredAt, ReadAt FROM ActivityDb.activity.Notifications ORDER BY Id;
      SELECT MerchantUserId, [Date], [Hour], PaymentCount, TotalAmount FROM ActivityDb.activity.MerchantHourlySummaries"'
```

## Failure drills (Demo Day)

Set up once: register a user and a merchant, top the user up, and log in as the merchant in a second
browser window (or a private window) with **Notifications** or **Daily summary** open.

### Drill 1: the same transfer twice

Double-click **Send** on the Send money page. One transfer, one notification. Sending the same request
again with the same `Idempotency-Key` returns `201` with `Idempotent-Replayed: true` and no new event.

### Drill 4: Kafka down during a transfer

```bash
docker compose stop kafka
curl http://localhost:8081/health          # "Degraded", kafka: "Kafka is unreachable."
```

Make a transfer in the UI: it returns **201 straight away** and the balance moves. The outbox row waits
(see "Looking at the databases": `SentAt` is NULL, `Attempts` goes up). No notification yet.

```bash
docker compose start kafka
docker compose logs -f wallet-api activity-service   # wait for "Published event ..." then "Processed event ..."
```

The event goes out about 6–10 s after Kafka is back, and the notification appears live in the merchant's
window within a second of that.

### Drill 5: the same event twice

Covered by the tests (`ConsumerIntegrationTests.SameEventTwice_OneNotificationAndNoDoubleCount`). In compose,
re-publish an event that was already processed:

```bash
# copy one TransferCompleted envelope from the Wallet outbox...
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -h -1 -y 0 \
  -Q "SET NOCOUNT ON; SELECT TOP 1 Payload FROM WalletDb.wallet.OutboxMessages ORDER BY Id DESC"' > /tmp/event.json
# ...and put it on the topic again
docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 \
  --topic paylite.transfers < /tmp/event.json
docker compose logs activity-service | grep "already processed"
```

The Activity Service logs `Event ... was already processed, skipping`. The notification count and the
merchant summary don't change.

### Drill 6: Activity Service down during transfers

```bash
docker compose stop activity-service
# make 3 transfers in the UI: all 201
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 \
  --describe --group activity-service   # LAG shows the 3 waiting events
docker compose start activity-service
docker compose logs -f activity-service  # 3 x "Processed event ...", within a second
```

Afterwards: one notification per transfer (nothing lost, nothing doubled), the summary total equals the sum
of the transfers, and the lag is back to 0.

### Drill 7: poison message

```bash
echo 'this is not json' | docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh \
  --bootstrap-server localhost:9092 --topic paylite.transfers
docker compose logs activity-service | grep "dead-letter\|paylite.transfers.dlt"
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 \
  --topic paylite.transfers.dlt --from-beginning --property print.headers=true --timeout-ms 5000
```

The Activity Service logs `Sent paylite.transfers partition N offset M to paylite.transfers.dlt (poison): The
message is not valid JSON.` The dead-letter topic has the original bytes with `dlt-reason:poison`,
`dlt-error`, `dlt-original-topic/partition/offset`, `dlt-failed-at` and `dlt-attempts`. Make another transfer:
it is processed normally, so the consumer kept going.

A transient error (e.g. `docker compose stop sqlserver` for a few seconds) is retried 3 times (1 s, 2 s, 4 s)
before the message is dead-lettered with `dlt-reason:retries-exhausted`.

## Rebuilding the Activity read model

`ActivityDb` is built only from events, and `paylite.transfers` keeps them forever (ADR-0004), so it can be
rebuilt from scratch:

```bash
docker compose stop activity-service
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "DELETE FROM ActivityDb.activity.ProcessedEvents; DELETE FROM ActivityDb.activity.Notifications; DELETE FROM ActivityDb.activity.MerchantHourlySummaries"'
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 \
  --group activity-service --topic paylite.transfers --reset-offsets --to-earliest --execute
docker compose start activity-service
```

Every event is processed again. Because processing is idempotent and the summary uses increments only, the
result is the same as before. Read state (`ReadAt`) is lost, since it isn't an event.

## Common problems

| Symptom | Check |
|---|---|
| `docker compose up` stops at `sqlserver` (Apple Silicon) | Give Docker at least 4 GB of memory; SQL Server runs under emulation. |
| A transfer works but no notification arrives | `curl localhost:8082/health` (Kafka?), consumer lag (above), and `docker compose logs activity-service` for `Sent ... to paylite.transfers.dlt`. |
| The bell doesn't update live | Browser dev tools → Network → `hubs/notifications`: it should be a WebSocket with status 101. A 401 means the access token expired: log in again. |
| `401` on every request after a while | Access tokens last 15 minutes and live in memory only (ADR-0003): log in again. A page refresh also logs you out. |
| Something on the dead-letter topic | Read it with the headers (Kafka section): `dlt-reason` and `dlt-error` say why. |
