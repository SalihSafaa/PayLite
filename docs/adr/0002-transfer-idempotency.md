# ADR-0002: How transfer idempotency works

- **Status:** Proposed
- **Date:** _TODO_
- **Deciders:** Owner A, Owner B

## Context

The frontend sends an `Idempotency-Key` (UUID) with every `POST /api/transfers`. Same key + same request must return the **original** result with no second transfer; same key + a **different** request must return `422`. Decide: where keys are stored, how "same request" is compared (e.g. a hash of the body), how a concurrent duplicate (two requests with the same key at the same moment) is handled, and how long keys are kept.

## Decision

_TODO (Day 2): agree on this together, then fill it in through a PR approved by both owners._

## Consequences

_TODO_
