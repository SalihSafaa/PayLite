# ADR-0003: Where the frontend stores tokens

- **Status:** Proposed
- **Date:** _TODO_
- **Deciders:** Owner A, Owner B

## Context

The brief requires the access token to be kept **in memory, not localStorage**, because any script injected through an XSS bug can read localStorage. Losing the session on a page refresh is accepted. The skeleton implements this in `frontend/src/shared/auth/tokenStore.ts`. Record the reasoning, the trade-off, and what happens to the refresh token (stretch goal: httpOnly cookie).

## Decision

_TODO (Day 2): agree on this together, then fill it in through a PR approved by both owners._

## Consequences

_TODO_
