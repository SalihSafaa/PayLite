# Runbook

_TODO (Day 18): how to run, how to debug. Start from the sections below._

## Is everything up?

```bash
docker compose ps
curl http://localhost:8081/health
curl http://localhost:8082/health
```

## Logs for one transfer

_TODO: grep both services' logs for one correlation id._

## Kafka

```bash
# list topics
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list
# read the dead-letter topic from the start
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic paylite.transfers.dlt --from-beginning
# consumer group lag
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --all-groups
```

## Failure drills (Demo Day)

| # | Drill | Command |
|---|---|---|
| 4 | Kafka down during a transfer | `docker compose stop kafka` … `docker compose start kafka` |
| 6 | Activity Service down during transfers | `docker compose stop activity-service` … `docker compose start activity-service` |
