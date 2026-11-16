# Scenarios 6 and 7: what I saw in compose

Status: Open
From: Salih (A) · To: Aboalhassan (B)

I rehearsed your two drills in compose with your consumer from #10, so we know where we stand.

## Scenario 6: Activity Service down, then catches up ✅ (works, needs a test)

```bash
docker compose stop activity-service
# make 3 transfers -> all 201, notifications in ActivityDb stay at 4
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 --describe --group activity-service   # LAG shows the waiting events
docker compose start activity-service
```

Within a second of starting, all 3 were processed, each with its correlation id:

```
[13:32:19 INF] Processed event 2f12...: transfer 5, 101.00 IQD to user 3 (correlation demo-activity-down-001)
[13:32:19 INF] Processed event 1cbf...: transfer 6, 102.00 IQD to user 3 (correlation demo-activity-down-002)
[13:32:19 INF] Processed event 69b7...: transfer 7, 103.00 IQD to user 3 (correlation demo-activity-down-003)
```

After: 7 notifications for 7 transfers (nothing lost or doubled), merchant summary 7 payments /
5,006 IQD = the sum of the 7 transfers, consumer lag 0. Could you add these steps to the runbook,
and an automated test for the README table (e.g. publish events while the consumer isn't running,
start it, assert one notification each)?

## Scenario 7: poison message ❌ (not done yet)

```bash
echo 'this is not json' | docker compose exec -T kafka \
  /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 --topic paylite.transfers
```

Your consumer logs `Poison message at paylite.transfers [[1]] @7, skipping it: The message is not valid JSON.`
and keeps going (the next real transfer was processed fine), but **`paylite.transfers.dlt` stays
empty** (`kafka-console-consumer ... --from-beginning` → 0 messages). Same as
`2026-11-15-A-to-B-consumer-dlt-retries.md`: it needs to be produced to the DLT with the `dlt-*`
headers, plus the 3-retries limit for transient errors.

Small thing: the log shows `[[1]]` (the partition is printed inside brackets twice).

**This is the one scenario that isn't working yet, and it's demoed live on Tuesday.** If you're short on
time, I'm happy to pair on it tomorrow morning; tell me here.
