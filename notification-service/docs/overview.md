# notification-service: overview

> Moved from `notification-service/CLAUDE.md` (PGTI-215), text unchanged except headings. A section named
> with "see ... below" or "§..." may now live in a sibling file in this folder: `grep -n '^## ' notification-service/docs/*.md` finds it.

## What this service is

`notification-service` is the platform's way out to email. It is a self-contained .NET 10
microservice with its own solution, `Dockerfile`, and PostgreSQL database (`notifications`).
Inside the Compose network it answers on `http://notification-service:8080`; from the host it
is published on `NOTIFICATIONS_PORT` (5190).

It receives a finished email — a from-address, a subject, an HTML body still carrying its
`{{placeholders}}`, a data bag and a list of addresses — substitutes, sends and records. It owns
sending, retry, the SMTP credential, the send history, the suppression list, and the read API
behind the client's history screen.

**Current state — it accepts, records and sends.** On top of the scaffold (health checks,
OpenAPI/Scalar, Serilog, Postgres wiring, in-memory `IDistributedCache`, `Result`-based error
handling, Keycloak with a fail-closed fallback policy, EF migrations at startup) the service
answers `POST /v1/notifications` end to end up to the `202` — the four tables, the validation
ladder, caller-minted idempotency settled by a unique index, the substitution engine in
`Core/Rendering/` — and **a real email now leaves over real SMTP**: `Core/Sending/` plus
`Web/Workers/NotificationDrainWorker`, the monorepo's first `BackgroundService`. **The send history
is readable**, too: three GET routes over the record, in `Core/History/` and `Web/GlobalEndpoints/`.
**And delivery feedback comes back**: the SendGrid event webhook lives in `Core/Webhooks/` plus
`Web/WebhookEndpoints/`, so rows move past `sent` and the suppression list finally has its writer.
events-service posts its four quote alerts (`06`) through it, which closes the queue in
`docs/specs/notifications/BUILD-ORDER.md`.

## Tech stack & conventions

The platform template, unmodified — `docs/dotnet-service-conventions.md` is the full reference, and
`events-service` is the fully-built sibling to copy patterns from. Particular to this service:

- One solution, `Sorteo360.Notifications.slnx`, with projects `.Core`, `.Infrastructure`,
  `.Web`, `.Tests.Unit`, `.Tests.Integration`, plus the chassis project referenced from
  `../shared/Sorteo360.Platform`.
- **No outbound `ServiceAuth__*` and no `Services__*`.** This service calls nobody, so it has no
  confidential client in the realm — a secret nobody uses is a credential to rotate for nothing.
- **It owns the monorepo's first `BackgroundService`** — `Web/Workers/NotificationDrainWorker`.
  None exists in any other service or in the chassis. It is a thin loop: the rules live in
  `Core/Sending/NotificationDrainService`, which is what the tests drive.

## Layer layout

```
<repo root>/
├── Directory.Packages.props / Directory.Build.props   # shared by every service (root, not copied)
├── shared/Sorteo360.Platform/           # Shared chassis — referenced, not copied
│   ├── Errors/  Database/  GlobalEndpoints/  Filters/  HealthEndpoints/
└── notification-service/                    # This solution (Sorteo360.Notifications.slnx)
    ├── Sorteo360.Notifications.Core/            # Domain: entities, services, DTOs (no deps on Infra/Web)
    │   ├── Cache/  Database/  DTOs/  Mappers/           # feature folders (add domain here)
    │   ├── Notifications/                               # the send: entities, service, vocabulary, fingerprint
    │   ├── Rendering/                                   # the substitution engine (pure, no I/O)
    │   ├── Sending/                                      # the drain rules: retry, suppression, the SMTP seam
    │   ├── Webhooks/                                     # the event webhook's rules: signature, ladder, correlation, dedupe (pure)
    │   ├── History/                                     # the read side: the tracking cap and the read scope (pure)
    │   ├── Shared/                                      # PagedResult<T>, the Core half of paging
    │   ├── Errors/                                      # notification error catalogs
    ├── Sorteo360.Notifications.Infrastructure/  # Persistence & external concerns (references Core)
    │   ├── Database/  NotificationsDbContext.cs, DesignTimeFactory, Configurations/, Migrations/
    │   ├── Smtp/     the real MailKit transport and its DI extension (AddSmtpSending)
    │   ├── Cache/  HealthChecks/
    ├── Sorteo360.Notifications.Web/             # Minimal API host / composition root (references Core + Infra)
    │   ├── Program.cs (composes the chassis), Auth/, Configuration/ (DomainServicesConfiguration only), OpenApi/
    │   ├── Workers/                                      # NotificationDrainWorker, the BackgroundService
    │   ├── NotificationEndpoints/                       # the send (POST) and the history (three GETs)
    │   ├── WebhookEndpoints/                            # the event webhook, the one anonymous third-party route
    │   ├── GlobalEndpoints/                             # PagedResponse + PagingRequest/Limits, copied from events-service
    │   ├── Filters/  Validators/                        # service-owned: the payload filter and its validator
    ├── Sorteo360.Notifications.Tests.Unit/          # Unit tests (mirror Core/Infrastructure)
    └── Sorteo360.Notifications.Tests.Integration/   # WebApplicationFactory integration tests
```

## Domain-shaped code

**The chassis gains nothing for this service:** everything it needs already ships there.

Domain-shaped things stay here: error catalogs in `Core/Errors/`, response shapes in
`Web/GlobalEndpoints/`, service-owned filters in `Web/Filters/`, the roles and policies in
`Web/Auth/`, and `DomainServicesConfiguration` — the only file in `Web/Configuration/`.

## Database

One schema, `notifications`, inside the service's own database. `infra/postgres/init.sh` creates
the **empty** database and nothing else; EF Core migrations are the single schema authority and
run at startup in every environment. Tables and columns are declared explicitly — `ToTable("<table>",
NotificationsDbContext.SchemaName)` and `HasColumnName` — never left to EF conventions.

```bash
dotnet ef migrations add <Name> \
  --project Sorteo360.Notifications.Infrastructure \
  --startup-project Sorteo360.Notifications.Web \
  --output-dir Database/Migrations
```

## Mailpit

`mailpit` is a **real SMTP server** running beside this service, and the production sending path
runs against it unchanged: the "switch" to real sending is `Smtp__Host`, and the only line that
differs is `Smtp__RequireTls`. It carries **no Compose profile**, because this service's
`depends_on` gates on its health and a profiled dependency breaks a bare `docker compose up`.

> **Mailpit is permanent.** It is the development and test sink forever, and stage's guardrail
> forever. It is **not** scaffolding to delete once SendGrid works (ADR-0020).

The local inbox is at `http://localhost:${MAILPIT_UI_PORT}` (8025).

## Growing the service

- **First domain model:** add the entity/service in `Core`, its `*Data` persistence class +
  `IEntityTypeConfiguration<T>` in `Infrastructure/Database/Configurations`, register the `DbSet` on
  `NotificationsDbContext`, then add a migration. Startup already applies pending migrations.
- **A new role** is a constant in `AuthorizationPolicies` plus two realm edits in the same PR:
  `infra/keycloak/pgti360-realm.json` for environments created later, and a console step in
  `docs/deploy-with-dokploy.md` for the ones already running (`docs/adr/0016`).
