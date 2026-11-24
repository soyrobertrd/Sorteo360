# Sorteo360 — Platform (Monorepo)

Platform-level guide: the rules that cross service boundaries, and where everything else
lives. Working **inside** a service? Its own `CLAUDE.md` is the authority on its internals.

## Map

| Where | What lives there | Read |
| --- | --- | --- |
| `events-service/` | Events, participant rosters, webhooks, QR/attendance sync, live prize raffles & notarial PDFs | `events-service/CLAUDE.md` |
| `convocatorias-service/` | Invitation campaigns, public registration portal, QR door check-in | `convocatorias-service/CLAUDE.md` |
| `notification-service/` | Outbound SMTP email delivery gateway and send history | `notification-service/CLAUDE.md` |
| `frontend/` | The SPA (Vite + React) | `frontend/CLAUDE.md` |
| `shared/Sorteo360.Platform/` | The shared technical chassis (`Sorteo360` / shared platform scaffolding) | `shared/CLAUDE.md` |
| root | Compose stack, infra, Caddy reverse proxy, backups, package versions | this file |

**Operations live in `README.md` and `docs/deploy-sorteo360.md`.**

## Stack

.NET 10 Minimal APIs, one container per service, port 8080 internally, non-root runtime user.
PostgreSQL via EF Core + Npgsql, **one database per service** (`events`, `convocatorias`,
`notifications`, `keycloak`) inside a single shared TLS-enabled instance. Docker Compose locally
and in production (`docker-compose.prod.yml` with Caddy TLS reverse proxy). Identity is Keycloak
(`docs/adr/0004`). Service-to-service is synchronous HTTP (`AddServiceClient` + Polly). Email
leaves through `notification-service` over SMTP (`Mailpit` in local dev, SMTP relay in prod).

## Local stack

```bash
docker compose up -d                        # everything (COMPOSE_PROFILES=all in .env)
COMPOSE_PROFILES=events docker compose up -d   # one service + infra (infra carries no profile)
docker compose build <svc> && docker compose up -d <svc>
docker compose logs -f <svc>
docker compose down -v                      # down + WIPE data (re-runs init.sh)
```

## Rules that cross service boundaries

- **Package versions are governed at the root.** Central Package Management lives in the root
  `Directory.Packages.props` — never keep a service-local copy.
- **The chassis is referenced, never copied.** All .NET services consume the shared chassis by
  `ProjectReference` (`docs/adr/0012`).
- **.NET images build from the repo-root context** (`build.context: .`,
  `dockerfile: <service>/Dockerfile`) so `ProjectReference` to `shared/` resolves.
- **EF Core migrations are the single schema authority.** `infra/postgres/init.sh` creates empty
  databases only; each service applies pending migrations on startup via `Database.MigrateAsync()`.
- **The Keycloak realm JSON seeds a realm; the console owns it afterwards** (`docs/adr/0016`).
  `infra/keycloak/sorteo360-realm.json` defines the `sorteo360` realm and `sorteo360-app` client;
  `docs/roles.md` documents the roles and `scripts/roles-parity.sh` checks alignment across the JSON,
  `docs/roles.md`, and `AuthorizationPolicies.cs`.
- **Errors carry a machine `code` alongside the HTTP status**, emitted by the chassis as a
  ProblemDetails extension.
- **Config is environment variables only** (`Section__Key` → `Section:Key`). Secrets come from
  `.env` (dev) or `.env.prod` (production) — never committed.
- **`/_alive` and `/_ready` stay `AllowAnonymous`.** `/_alive` gates Docker healthchecks.
- **Domain names are English platform-wide** (`docs/adr/0008`); Spanish prose in UI, logs, and
  comments (`cedula` exception applies per ADR-0008).

## Runbooks & reference

| Need | Read |
| --- | --- |
| **What is pending and where to resume (read first)** | `plans/bloques-pendientes.md` → "Estado al …" |
| Security audit run-1: findings and how to resume | `docs/security-audit-2026-11.md` |
| Production deployment, TLS & backups | `docs/deploy-sorteo360.md` |
| PaaS trial deploy (Railway / Fly.io) — plan, not yet done | `docs/deploy-paas-railway-fly.md` |
| Every app role: what it does, who holds it | `docs/roles.md` |
| Architecture decisions & service specs | `docs/adr/`, `docs/specs/` |
| Domain contexts and how they relate | `GLOSSARY-MAP.md` → `GLOSSARY.md` |
| Events & Raffles API architecture | `events-service/docs/raffles.md`, `docs/ARCHITECTURE.md` |
