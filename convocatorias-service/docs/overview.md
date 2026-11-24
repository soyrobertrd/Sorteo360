# convocatorias-service: overview

> Moved from `convocatorias-service/CLAUDE.md` (PGTI-212), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' convocatorias-service/docs/*.md` finds it.
> Spec numbers (`03`, `04` §1, ...) are files in `docs/specs/convocatorias/`.

## What this service is

`convocatorias-service` manages the platform's *convocatorias*. It is a self-contained
.NET 10 microservice with its own solution, `Dockerfile`, and PostgreSQL database
(`convocatorias`). Inside the Compose network it answers on `http://convocatorias-service:8080`;
from the host it is published on `CONVOCATORIAS_PORT` (5185).

**Current state — foundation, the template catalogue, the campaign model, recipient uploads, a
send that completes, and the first path through the public portal.** The service boots with the full
cross-cutting plumbing (health checks, validation filter, OpenAPI/Scalar, Serilog, Postgres wiring,
in-memory `IDistributedCache`, `Result`-based error handling), **Keycloak authentication with a
fail-closed fallback policy**, a rate-limited public prefix, and EF migrations applied at startup. On
top of that sit four slices of domain: the `template` table with its five seeded `es` rows, one read
route and the substitution engine; the three campaign tables with both state machines, recipient
selection from CRM, and launch; the two staging tables behind the spreadsheet upload, with the
handoff worker behind the launch; and the `registration` table with the two anonymous routes that
resolve a token and write an inscription. **The service now does its whole primary job end to end on
either transport**: the stub with its table, or the real typed client against notification-service
(`Notifications__Transport=http`, which needs the realm steps in `docs/deploy-with-dokploy.md`). The ticket page and its
QR, unsubscribe, the registration window, the cancellation notice and the per-event tracking rollup
are built. The queue that fills them is
`docs/specs/convocatorias/BUILD-ORDER.md`.

The registration confirmation email is built: a successful registration mails the person their
codes synchronously inside the POST, through a **transactional** send transport that mirrors the
campaign one — see "The confirmation email" below.

**The send runs on a worker, and behind it is one of two transports.** Launch flushes
unsubscribes to CRM, re-checks the whole recipient list against it, reads the event and refuses on
what it says, refuses a shut registration window, stamps the event's three values on the campaign
row, flips it to `sending`, writes its id onto an in-process channel and answers `202`. A `BackgroundService` drains that channel, reads
the campaign's template, mints each recipient's two tokens, assembles a complete `SendBatchRequest`,
hands it to `ICampaignSendGateway` in chunks of 1,000, records what came back per recipient and
lands the campaign on `sent` when no row is left `pending`
(`docs/specs/convocatorias/04-send-and-handoff.md` §1.1, ADR-0018). What implements that gateway is
`StubCampaignSendGateway` or `HttpCampaignSendGateway` — see "The send transport" below. When a
batch call fails the rows stay `pending`, and the two routes that unstick that are
`resume-handoff` and `retry-failed` — see "Resume and retry" below. What the event read still does not do is
pick the template by the event's language, which is why `language` arrives in the create body instead
of being taken from the event — and that value is now on the wire, so `02` §3's "dictated by the
Event" is a gap with contract weight rather than a local shortcut.

## Tech stack & conventions

The platform template, unmodified — `docs/dotnet-service-conventions.md` is the full reference,
and `events-service` is the fully-built sibling to copy patterns from. Only two things are
particular to this service:

- One solution, `Sorteo360.Convocatorias.slnx`, with projects `.Core`, `.Infrastructure`,
  `.Web`, `.Tests.Unit`, `.Tests.Integration`, plus the chassis project referenced from
  `../shared/Sorteo360.Platform`.
- Four domain models so far — the template catalogue, campaigns, recipient uploads and
  registrations — so the conventions about entities and entity configuration have worked examples
  here. See "The template catalogue", "The campaign model", "Recipient uploads" and "The
  registration portal" below.

## Layer layout

```
<repo root>/
├── Directory.Packages.props / Directory.Build.props   # shared by every service (root, not copied)
├── shared/Sorteo360.Platform/           # Shared chassis — referenced, not copied
│   ├── Errors/  Database/  GlobalEndpoints/  Filters/  HealthEndpoints/
└── convocatorias-service/                   # This solution (Sorteo360.Convocatorias.slnx)
    ├── Sorteo360.Convocatorias.Core/            # Domain: entities, services, DTOs (no deps on Infra/Web)
    │   ├── Templates/   Template, TemplateKey, TemplatePurpose, TemplateSeed (the seeded copy)
    │   ├── Campaigns/   Campaign, CampaignRecipient, CampaignExclusion, the two state catalogues
    │   ├── Crm/         ICrmContactDirectory and the contact shape the domain sees
    │   ├── Rendering/   the substitution engine, the supported placeholder set, the body rules
    │   ├── Portal/     the invitation links, the token the portal runs on, and where it hangs from
    │   ├── Registrations/ the registration, its two codes, the two token policies, the CRM sync
    │   ├── Uploads/     the upload policy, the column map, the row classifier, the staging entities
    │   ├── Identity/    ICurrentUser
    │   ├── Shared/      AllowedLanguage, PagedResult, EmailAddressShape, PersonNameShape
    │   ├── Cache/  Database/  DTOs/  Mappers/           # feature folders (add domain here)
    │   ├── Errors/                                      # convocatorias error catalogs
    ├── Sorteo360.Convocatorias.Infrastructure/  # Persistence & external concerns (references Core)
    │   ├── Database/  ConvocatoriasDbContext.cs, DesignTimeFactory, Configurations/, {Entity}Data, repositories
    │   ├── Database/Migrations/                         # the schema's single authority
    │   ├── Crm/       the typed contacts client and the directory that degrades to 503
    │   ├── Handoff/   the in-memory channel the launch writes and the worker drains
    │   ├── Neighbours/ NeighbourPageReader — the one page loop both directories read through;
    │   │               NeighbourCall — where any neighbour call's transport failure becomes a Result
    │   ├── Uploads/   the xlsx/csv reader and the decompression budget
    │   ├── Cache/  HealthChecks/
    ├── Sorteo360.Convocatorias.Web/             # Minimal API host / composition root (references Core + Infra)
    │   ├── Program.cs (composes the chassis), Configuration/ (DomainServicesConfiguration only), OpenApi/, Validators/
    │   ├── TemplateEndpoints/                           # one read route over the template catalogue
    │   ├── CampaignEndpoints/                           # the ten campaign routes + the two upload routes
    │   ├── RegistrationWindowEndpoints/                 # close-registrations / reopen-registrations
    │   ├── CancellationNoticeEndpoints/                 # cancellation-notice, the fan-out to registrants
    │   ├── ParticipantEndpoints/                        # the participants list and its CSV, one row per person
    │   ├── ReconciliationEndpoints/                     # crm-sync / unsubscribe-sync retries, the five buckets, exclusions, acknowledge-details
    │   ├── TrackingEndpoints/                           # the per-event rollup, campaigns embedded
    │   ├── CheckInEndpoints/                            # the events a CRM contact attended, by contact id
    │   ├── Handoff/                                     # CampaignHandoffWorker, the BackgroundService
    │   ├── Filters/                                     # service-owned filters — empty placeholder today
    ├── Sorteo360.Convocatorias.Tests.Unit/          # Unit tests (mirror Core/Infrastructure)
    └── Sorteo360.Convocatorias.Tests.Integration/   # WebApplicationFactory integration tests
```

## Shared platform chassis: what stays here

Domain-shaped things stay here: convocatorias error catalogs in `Core/Errors/`, service-owned
filters in `Web/Filters/`, and `DomainServicesConfiguration` — the only file left in
`Web/Configuration/`. `Web/Filters/` is still an empty placeholder; the chassis-owned
`Web/GlobalEndpoints/` and `Web/HealthEndpoints/` are gone entirely.

**Authentication is opt-in and this service takes it.** `Program.cs` calls the chassis
`AddKeycloakAuthentication`; the roles, the policies and the fallback policy are this service's own
(`Web/Auth/AuthorizationPolicies.cs`), because role names are domain and never move to the chassis.

## Errors

The one `ErrorKind.Forbidden` here is `OPT_NOT_ASSIGNED`, on selection by contact list. The before/after of the adoption is in
`.scratch/platform-chassis-extraction/convocatorias-behaviour-check.md`.

## Endpoints outside the domain

Endpoints: `/_alive`, `/_ready`, `/_health`; `/scalar` and `/openapi/v1.json` for the API docs
(dev/stage). Those are the only anonymous routes outside the public prefix, and
`PublicSurfaceGuardTests` enumerates them one by one. What keeps the docs off the internet is
the network, not a role: the frontend's nginx forwards the two API prefixes and nothing else,
and this service has no hostname of its own.

## Growing the service

- **A new domain model:** add the entity/service in `Core`, its `*Data` persistence
  class + `IEntityTypeConfiguration<T>` in `Infrastructure/Database`, register the
  `DbSet` on `ConvocatoriasDbContext`, then `dotnet ef migrations add <Name>`. Startup already
  applies pending migrations in every environment. A table names its schema explicitly:
  `ToTable("<table>", ConvocatoriasDbContext.SchemaName)`.
- **A new endpoint** goes under one of the two prefixes in `Web/Routing/ApiRoutes.cs` and
  nowhere else. Anything outside `v{version:apiVersion}/public` is protected by the fallback
  policy unless it says otherwise, and `PublicSurfaceGuardTests` fails the build if a stray
  `.AllowAnonymous()` lands outside the public prefix.
- **A new role** is a constant in `AuthorizationPolicies` plus two realm edits in the same PR:
  `infra/keycloak/pgti360-realm.json` for environments created later, and a console step in
  `docs/deploy-with-dokploy.md` for the ones already running (`docs/adr/0016`). **Deleting a
  role is no different** — both edits, plus a delete step in the deploy doc for the realms that
  already have it, as the `campaigns:submit` / `campaigns:approve` removal did.
