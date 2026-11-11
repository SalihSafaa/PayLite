# Architecture

```
            ┌──────────────────────────────┐
            │   Frontend (React + TS)      │   served by nginx on :3000,
            │   login · wallet · send ·    │   which also proxies /api and /hubs
            │   history · notifications ·  │
            │   merchant dashboard         │
            └───────┬───────────────┬──────┘
         REST (JWT) │               │ REST + SignalR (JWT)
                    ▼               ▼
   ┌────────────────────┐     ┌──────────────────────┐
   │   Wallet API :8081 │     │ Activity Service     │
   │   (Owner A)        │     │ :8082 (Owner B)      │
   └───────┬────────────┘     └──────▲───────┬───────┘
           │ T-SQL proc + outbox     │       │
           ▼                         │       ▼
   ┌───────────────┐   ┌─────────┐   │  ┌──────────────┐
   │ SQL Server    │──▶│  Kafka  │───┘  │ SQL Server   │
   │ WalletDb      │   │ (events)│      │ ActivityDb   │
   └───────────────┘   └─────────┘      └──────────────┘
```

## Rules

- Each service owns its own database (`WalletDb`, `ActivityDb`) on the same SQL Server instance.
  This is enforced, not just agreed: `wallet_app` can only open `WalletDb` and `activity_app`
  can only open `ActivityDb` (`db/init-app-logins.sql`).
- Services talk only through Kafka events (`src/Shared.Contracts`).
- The Wallet API issues JWTs; both services validate the same token (`JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`).

## Request routing (one origin for the browser)

| Path | Service |
|---|---|
| `/api/notifications*` | Activity Service |
| `/api/merchants/me/daily-summary` | Activity Service |
| `/hubs/*` (WebSockets) | Activity Service |
| everything else under `/api/` | Wallet API |

Defined twice and kept identical: `frontend/nginx.conf` (Docker) and `frontend/vite.config.ts` (dev).
