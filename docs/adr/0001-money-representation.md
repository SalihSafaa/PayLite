# ADR-0001: How money is represented

- **Status:** Proposed
- **Date:** 2026-11-12
- **Deciders:** Owner A, Owner B

## Context

Balances must never be wrong. The same amount crosses four boundaries: SQL Server, C#, JSON (the REST API and the Kafka event), and the UI. Each one can lose precision (`float`/`double`, JavaScript `number` for very large values, rounding) or mix up currencies. In practice the Iraqi dinar has no fractional unit: nobody pays 0.50 IQD, and the smallest banknote is 250 IQD. ISO 4217 still gives IQD 3 decimal places, but they are not used.

## Decision

**An amount is always a pair: `(amount, currency)`.** Every wallet is IQD for now, but the currency always travels with the amount, so adding a second currency later will not mean guessing what a number means.

| Boundary | Type | Rule |
|---|---|---|
| SQL Server | `decimal(18,2)` for every amount, balance, and total, in both `WalletDb` and `ActivityDb`. Currency is `char(3)`. | `CHECK (Balance >= 0)` on wallets, `CHECK (Amount > 0)` on transfers and top-ups. Never `float`, `real`, or `money`. |
| C# | `decimal` | No `double` anywhere money is involved. No rounding: an amount that is not valid is rejected, never rounded. |
| JSON (REST + Kafka) | A JSON number (`25000`), plus a `currency` string (`"IQD"`) | Serialized with `EventJson.Options` / the default ASP.NET options. |
| UI | `number` for display only | Always formatted with the one `formatMoney()` in `frontend/src/shared/lib/money.ts` (`Intl.NumberFormat`, no fraction digits for IQD). The UI never adds up money: totals and balances come from the server. |

**IQD amounts must be whole dinars.** The API rejects a fractional IQD amount with `400` (validation: `amount % 1 == 0`), and the minimum is `1`. The UI validates the input as a whole number (`/^\d+$/`) before it sends it. A `decimal(18,2)` column still holds whole numbers without any loss, and leaves room for a currency with 2 minor digits.

**Limits keep every value a safe JavaScript integer.** One top-up is at most 1,000,000 IQD (`Wallet:MaxTopUpAmount`), and one transfer has a configurable limit as well. `decimal(18,2)` goes up to about `10^16`, and `Number.MAX_SAFE_INTEGER` is about `9 × 10^15`. No realistic balance or merchant daily total comes close to either, so a JSON number is safe and we do not need to send amounts as strings.

## Consequences

- One type per layer and no rounding code to get wrong. A bad amount fails loudly with a 400.
- The `CHECK` constraint is the last line of defence: even a buggy procedure cannot produce a negative balance.
- The Activity Service stores `amount` and `currency` exactly as they arrive in the event, using the same `decimal(18,2)`, so its totals match the Wallet API's to the dinar.
- What we give up: real fils (1/1000 IQD) cannot be represented. If we ever need them, that takes a new ADR and a migration to `decimal(19,3)`, and the event `version` gets bumped.
- Follow-up for PR #1: change the top-up minimum from `0.01` to `1` and reject fractional IQD amounts. The frontend should validate a whole number instead of using `Number(amount)` on free text.
