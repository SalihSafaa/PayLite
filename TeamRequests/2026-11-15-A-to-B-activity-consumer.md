# Activity Service consumer: what the Wallet side sends you

Status: Open
From: Salih (A) · To: Aboalhassan (B)

The Wallet side of the event flow is done once the outbox PR merges: every successful transfer
writes a `TransferCompleted` event in the same transaction, and a background publisher sends it to
Kafka (`Acks=All`, idempotent producer). If Kafka is down, events wait and go out when it's back.

You can build against this now:

- The payload is exactly your contract from the `DevBDay4` PR (`fromUserId`, `fromUsername`,
  `toUserId`, no `toUsername`). The outbox PR is built on top of it.
- Topic `paylite.transfers`, 3 partitions, key = `toWalletId` (string).
- Value = the JSON envelope from `docs/event-contract.md`. Deserialize it with `EventJson.Options`
  into `EventEnvelope<TransferCompletedData>`.
- `eventId` is fixed per transfer, so a re-publish after a crash sends the **same** `eventId`.
  That's the duplicate `ProcessedEvents` has to absorb.
- Headers: `X-Correlation-ID` (log it on every line) and `event-type`.
- In tests, the Wallet factory creates both topics; the same `AdminClient` code works for yours.

## Questions

1. Your standup says the consumer is next on `DevBDay5`. When do you expect that PR? Scenarios 5,
   6 and 7 and the live notification all depend on it, and they're demoed live.
2. Do you want me to add a small dev endpoint or script that publishes a sample event, so you can
   test without making real transfers?
