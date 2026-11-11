# ADR-0004: Kafka topic, partition key, and what "delivered" means

- **Status:** Proposed
- **Date:** _TODO_
- **Deciders:** Owner A, Owner B

## Context

Events flow Wallet API (outbox publisher) → Kafka → Activity Service (consumer). Decide:
- the topic name(s) and partition count (proposal in the skeleton: `paylite.transfers` + `paylite.transfers.dlt`, 3 partitions; see `Shared.Contracts/Kafka/Topics.cs` and the `kafka-init` service);
- the partition key (hint from the brief: what key keeps all events for one wallet in order?);
- for the **producer**: when is an outbox row "sent"? (acks, retries, idempotent producer);
- for the **consumer**: when is an offset committed? What goes to the dead-letter topic, after how many retries?

## Decision

_TODO (Day 2): agree on this together, then fill it in through a PR approved by both owners._

## Consequences

_TODO_
