# ADR-0001: How money is represented

- **Status:** Proposed
- **Date:** _TODO_
- **Deciders:** Owner A, Owner B

## Context

Balances must never be wrong. The same amount crosses four boundaries: SQL Server, C#, JSON (the Kafka event and the REST API) and the UI. Each boundary is a chance to lose precision (`float`/`double`, JavaScript `number` for very large values) or to mix currencies. Decide the type, precision and currency handling at **each** boundary.

Starting points already in the skeleton (change them if the ADR decides otherwise):
- `TransferCompletedData.Amount` is `decimal`, `Currency` is a string (`"IQD"`).
- `frontend/src/shared/lib/money.ts` formats with `Intl.NumberFormat` and shows no fraction digits for IQD.

## Decision

_TODO (Day 2): agree on this together, then fill it in through a PR approved by both owners._

## Consequences

_TODO_
