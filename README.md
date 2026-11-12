# PayLite

A mini digital wallet, built as a team: users top up, send money to each other or pay a merchant,
and the recipient gets a live notification in the browser. Two .NET services, one event in between,
a React frontend, all running in containers.

> **Status: skeleton.** Auth, infrastructure, CI, the frontend shell and the contracts are in place.
> The feature work (wallets, transfers, outbox, consumer, notifications, merchant summary) is
> marked `TODO (Owner A)` / `TODO (Owner B)` in the code.

## Architecture

See [`docs/architecture.md`](docs/architecture.md) for the diagram and the routing table.

- **Wallet API** (`src/Wallet.Api`, Owner A): auth, wallets, transfers (T-SQL procedure), idempotency, transactional outbox → Kafka.
- **Activity Service** (`src/Activity.Service`, Owner B): Kafka consumer, notifications, merchant daily summary, SignalR.
- **Shared.Contracts** (`src/Shared.Contracts`): the event envelope, payloads, topic and header names. Nothing else.
- **Frontend** (`frontend`): React + TypeScript (strict) + Vite, served by nginx.
- Each service owns its own database (`WalletDb`, `ActivityDb`) and connects with its own least-privilege login.

## Who owns what

| | Owner A: "Money in motion" | Owner B: "Reacting to events" |
|---|---|---|
| Person | _TODO (Day 1)_ | _TODO (Day 1)_ |
| Service | Wallet API | Activity Service |
| Database | `WalletDb` | `ActivityDb` |
| Kafka | Producer (outbox + publisher) | Consumer (processed events, retries, dead-letter) |
| Frontend | `src/features/wallet`: dashboard, send money, history | `src/features/activity`: bell, notifications, merchant payments, daily summary |
| Failure scenarios | 1, 2, 3, 4 (with B) | 4 (with A), 5, 6, 7 |

### Shared work split

_TODO (Day 1): one name per row. Nothing should be "we both kind of did it"._

| Shared item | Owner |
|---|---|
| Event contract (`Shared.Contracts`, `docs/event-contract.md`) | |
| ADR-0001 money · ADR-0002 idempotency · ADR-0003 tokens · ADR-0004 Kafka | |
| `docker-compose.yml`, Dockerfiles, `db/init-app-logins.sql` | |
| CI (`.github/workflows/ci.yml`) | |
| Frontend shell: login, register, routing, API clients, token handling, error component | |
| Correlation ID end to end | |
| README, `docs/runbook.md` | |
| Demo Day script | |

## Tech stack

- .NET 10, ASP.NET Core Web API (controllers), EF Core + SQL Server 2022
- JWT bearer auth, Argon2id password hashing, refresh tokens with rotation and reuse detection
- Kafka (`apache/kafka`, KRaft, no ZooKeeper) with `Confluent.Kafka`
- SignalR (Activity Service → browser)
- Serilog, Swagger / OpenAPI, health checks (SQL Server + Kafka)
- xUnit, Testcontainers (`Testcontainers.MsSql`, `Testcontainers.Kafka`), `WebApplicationFactory`
- React 19, TypeScript strict, Vite, React Router, `openapi-typescript` + `openapi-fetch`, `@microsoft/signalr`, oxlint
- Docker Compose, GitHub Actions

Component library: _TODO (Day 1): pick MUI or Mantine together._ Until then the shell uses a few plain CSS rules in `frontend/src/index.css`.

## Repository layout

```
src/
  Shared.Contracts/        event envelope, TransferCompleted v1, topic + header names
  Wallet.Api/              Controllers, Services, Interfaces, DTOs, Models, Data, Migrations, Health, ...
  Activity.Service/        same layout
tests/
  Wallet.Api.Tests/        integration tests: real SQL Server + Kafka via Testcontainers
  Activity.Service.Tests/
frontend/
  src/app/                 routes, login, register
  src/shared/              api clients (generated types), auth (in-memory token), components, money formatting
  src/features/wallet/     Owner A pages
  src/features/activity/   Owner B pages
db/init-app-logins.sql     creates wallet_app / activity_app, each limited to its own database
docs/                      architecture, event contract, ADRs, standups, runbook
docker-compose.yml
```

## Prerequisites

- Docker Desktop (required to run the stack **and** the integration tests). On Apple Silicon give Docker at least 4 GB of memory: SQL Server runs under emulation.
- .NET 10 SDK and Node 22, only to run things outside Docker.

## Configuration

Everything comes from environment variables. Copy the template and fill it in:

```bash
cp .env.example .env
```

| Variable | Purpose |
|---|---|
| `DB_PASSWORD` | SQL Server `sa` password (8+ chars: upper, lower, digit, symbol). Used only by SQL Server, the migrators and `dbsetup`. |
| `WALLET_DB_PASSWORD` | Password of `wallet_app`, the login the Wallet API uses. No single quotes. |
| `ACTIVITY_DB_PASSWORD` | Password of `activity_app`, the login the Activity Service uses. No single quotes. |
| `JWT_KEY` | Signing key, 32+ characters. The Wallet API signs with it, the Activity Service validates with it. |
| `JWT_ISSUER`, `JWT_AUDIENCE` | Optional, default `PayLite`. Must be the same for both services (compose guarantees this). |
| `ADMIN_SEED_USERNAME`, `ADMIN_SEED_PASSWORD` | First admin account, created by the Wallet API at startup if no admin exists. |
| `ASPNETCORE_ENVIRONMENT` | Optional, default `Development` (Swagger only in Development). |

`.env` is git-ignored. Never commit it.

## Run with Docker Compose

```bash
docker compose up --build
```

Start order (each step waits for the previous one):

1. **sqlserver** and **kafka** start and become healthy.
2. **kafka-init** creates the topics, then exits.
3. **wallet-migrator** and **activity-migrator** apply each service's EF Core migration bundle as `sa`, then exit.
4. **dbsetup** creates `wallet_app` and `activity_app` with only the rights each service needs, then exits.
5. **wallet-api** and **activity-service** start, connecting with their own logins.
6. **frontend** (nginx) serves the app and proxies `/api` and `/hubs` to the right service.

| URL | What |
|---|---|
| http://localhost:3000 | Frontend |
| http://localhost:8081/swagger | Wallet API Swagger |
| http://localhost:8082/swagger | Activity Service Swagger |
| http://localhost:8081/health, http://localhost:8082/health | Health (JSON, per dependency) |
| `localhost,1433` | SQL Server, for SSMS / the VS Code MSSQL extension |
| `localhost:29092` | Kafka, from your machine |

Wipe everything and start over: `docker compose down -v`.

## Run locally (services with `dotnet run`, frontend with Vite)

Start the infrastructure in Docker, then the services on your machine:

```bash
docker compose up -d sqlserver kafka kafka-init wallet-migrator activity-migrator
```

```bash
dotnet user-secrets --project src/Wallet.Api set "ConnectionStrings:WalletDb" "Server=localhost,1433;Database=WalletDb;User Id=sa;Password=<DB_PASSWORD>;TrustServerCertificate=True"
dotnet user-secrets --project src/Wallet.Api set "Jwt:Key" "<same JWT_KEY as below>"
dotnet user-secrets --project src/Wallet.Api set "Seed:AdminUsername" "admin"
dotnet user-secrets --project src/Wallet.Api set "Seed:AdminPassword" "<admin password>"
dotnet user-secrets --project src/Activity.Service set "ConnectionStrings:ActivityDb" "Server=localhost,1433;Database=ActivityDb;User Id=sa;Password=<DB_PASSWORD>;TrustServerCertificate=True"
dotnet user-secrets --project src/Activity.Service set "Jwt:Key" "<same JWT_KEY as above>"
```

```bash
dotnet run --project src/Wallet.Api          # http://localhost:8081
```

```bash
dotnet run --project src/Activity.Service    # http://localhost:8082
```

```bash
cd frontend && npm install && npm run dev    # http://localhost:5173 (proxies to 8081 / 8082)
```

### Migrations

The services never migrate at startup (with several replicas, every instance would race to change the
same schema; and the app's login would need schema-changing rights). Add a migration with the local tool:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Wallet.Api -o Migrations
dotnet ef database update --project src/Wallet.Api
```

## Regenerating the frontend API clients

The TypeScript types for both APIs are generated from their OpenAPI documents; nobody hand-writes them.
With both services running:

```bash
cd frontend && npm run gen:api
```

Commit the regenerated files in `frontend/src/shared/api/generated/` together with the API change.

## Run the tests

```bash
dotnet test PayLite.slnx
```

Docker must be running: each test class starts a real SQL Server and a real Kafka with Testcontainers,
applies the real migrations, and throws them away afterwards. Integration tests never use the EF
InMemory provider: it has no locks, no stored procedures and no key violations.

Frontend checks:

```bash
cd frontend && npm run lint && npm run build
```

## How we work

- `main` is protected; everything goes through a PR reviewed by the other owner, with CI green.
- Keep PRs under ~400 changed lines. Use the PR template, and declare help or borrowed/AI-generated code.
- Contracts before code: changes to `src/Shared.Contracts` need both owners' approval.
- Daily notes in [`docs/standups.md`](docs/standups.md). Decisions in [`docs/adr/`](docs/adr).

## Failure scenarios

| # | Scenario | Expected result | Owner | Test |
|---|---|---|---|---|
| 1 | The same transfer is submitted twice | Exactly one transfer | A | _TODO_ |
| 2 | 20 parallel transfers drain a wallet | Balance never negative; the right number succeed | A | _TODO_ |
| 3 | A→B and B→A at the same moment | Both complete, no deadlock | A | _TODO_ |
| 4 | Kafka stopped, transfer made, Kafka restarted | Transfer succeeds; notification arrives later | A + B | _TODO_ |
| 5 | The same event delivered twice | One notification; summary not double-counted | B | _TODO_ |
| 6 | Activity Service down during transfers, then restarted | Catches up; nothing lost or doubled | B | _TODO_ |
| 7 | Poison message on the topic | Goes to the dead-letter topic; consumer keeps going | B | _TODO_ |

## Concurrency

**Lock ordering (scenario 3).** Test: 20 A→B and 20 B→A transfers fired at the same moment.

- With lowest-id-first locking: 40 × 200, both balances unchanged, 40 transfer rows.
- With “lock the sender first”: 22 × 200, 18 × 500. Each 500 was SQL Server error 1205 (deadlock victim). A→B held A and waited for B while B→A held B and waited for A. SQL Server broke each cycle by killing one transaction. No money was lost, but valid transfers failed at random.

**Scenario 2.** A wallet with 10,000 and 20 parallel transfers of 1,000: exactly 10 × 200 and 10 × 409, the sender ends at 0 and is never negative, the recipient ends at 10,000, and there are 10 transfer rows. `UPDLOCK` makes the second request wait before it reads the balance, so nobody reads a stale value.

## Where the skeleton came from

The auth code (users, JWT, Argon2, refresh tokens), the `Result<T>` pattern, the exception handler,
the audit fields, the Dockerfile/compose pattern (migration bundle + one-shot `dbsetup` + least-privilege
login) and the Testcontainers factory are carried over from our Task 3/4 Product Catalog API. The
skeleton itself was scaffolded with AI assistance (Claude Code).
