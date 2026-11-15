# Event contract

The code version of this contract lives in `src/Shared.Contracts`. Both services reference that
project, so the producer and the consumer compile against the same types. **Changing it changes
both sides**: open a PR and get both owners' approval first.

## Envelope (`EventEnvelope<TData>`)

```json
{
  "eventId": "8f1c2e2a-...",
  "eventType": "TransferCompleted",
  "version": 1,
  "occurredAt": "2026-10-20T09:15:00Z",
  "correlationId": "b7d4...",
  "data": { }
}
```

| Field | Meaning |
|---|---|
| `eventId` | Unique per event. The consumer stores it in `activity.ProcessedEvents` (primary key) to detect duplicates. |
| `eventType` | One of `EventTypes` (`TransferCompleted`). |
| `version` | Version of `data`'s schema, so it can change later without breaking the consumer. |
| `occurredAt` | When the transfer happened, UTC. |
| `correlationId` | From the original HTTP request (`X-Correlation-ID`), so one transfer is traceable through every log line. |

Serialize with `EventJson.Options` on both sides (camelCase).

## `TransferCompleted` v1 (`TransferCompletedData`)

```json
{
  "transferId": 1042,
  "fromWalletId": 11,
  "fromUserId": 5,
  "fromUsername": "ali",
  "toWalletId": 27,
  "toUserId": 9,
  "toIsMerchant": true,
  "amount": 25000,
  "currency": "IQD"
}
```

`fromUserId`, `fromUsername` and `toUserId` are there because the Activity Service never reads `WalletDb`: `toUserId` is the JWT user id of the person to notify (and the merchant for the daily summary), and `fromUsername` is shown in the notification text.

## Topics and keys

See [ADR-0004](adr/0004-kafka-topic-and-delivery.md) (proposed).

| Topic | Key | Partitions |
|---|---|---:|
| `paylite.transfers` | `toWalletId` (string) | 3, retention forever |
| `paylite.transfers.dlt` | Original key | 3, retention 14 days |
