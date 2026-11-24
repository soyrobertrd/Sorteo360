# 00 — Foundation: the service, the realm, the database, the config

The scaffold. Nothing in this spec sends an email; everything after it assumes this is done.

**Read `docs/adding-a-new-service.md` first and work it step by step.** This spec does not restate
that runbook — it records the notification-specific answer at each of its nine steps, plus the two
things the runbook does not cover (the realm detail and the full configuration table). A spec that
assumes the runbook instead of carrying it produces a service that does not build, which is why
every step below names its file.

## Decisions behind this shape

- **The service is standalone with its own database** because the client's browsable send history is
  domain, and the root `CLAUDE.md` charters the shared chassis for technical scaffolding only.
  ADR-0020.
- **It holds zero domain knowledge.** No OPT, no evento, no cotización, no template table, no copy.
  That is what makes the rest of these specs short.
- **It calls nobody**, so it has no outbound confidential client and no `ServiceAuth__*` variables.
  A secret nobody uses is a credential to rotate for nothing.

## 1. Scaffold the folder

`notification-service/` at the repo root, same Clean Architecture layout as `events-service` —
`Core` / `Infrastructure` / `Web` plus `Tests.Unit` / `Tests.Integration` — and its own
`Sorteo360.Notifications.slnx`.

**Copy the layout, not the chassis.** The folders stay because the service keeps its own
domain-shaped things in them (`Core/Errors/` for its error catalog, `Web/GlobalEndpoints/` for
`PagedResponse`). The chassis files listed in the runbook's step 1 must **not** come across.

**No per-service `Directory.Build.props` or `Directory.Packages.props`.** MSBuild walks up and stops
at the first pair it finds, so a service copy silently shadows the root pair. New package versions
go in the root `Directory.Packages.props`.

## 2. Reference the chassis

`ProjectReference` to `shared/Sorteo360.Platform` from `Core`, `Infrastructure` and `Web`, and
its `.csproj` listed in the `.slnx` next to them. `Program.cs` composes from `AddWebServices`,
`AddDataServices<NotificationsDbContext>` and `AddKeycloakAuthentication`, leaving
`DomainServicesConfiguration` as the only class in `Web/Configuration/`.

**The chassis gains nothing for this service.** Everything it needs — `AddServiceClient`,
`ClientCredentialsTokenProvider`, `ServiceResiliencePipeline`, `BaseError`, `DbResult`, `IEndpoint`,
the health endpoints — already ships.

## 3. Dockerfile

Copy `events-service/Dockerfile`, adjust project names. Build context is the **repo root**, so
paths inside are written from the root and it COPYs `shared/` alongside its own folder. No
per-service `.dockerignore`. Build with `docker compose build notification-service`, never a bare
`docker build` from the service folder.

## 4. Compose block

`build.context: .` with `dockerfile: notification-service/Dockerfile`, the environment from §9,
`depends_on` on postgres, keycloak **and mailpit** health, and `profiles: [all, notifications]`.

**Mailpit is a second container and it takes no profile at all.** notification-service depends on
it, and a profiled dependency breaks a bare `docker compose up`. See `03` for its configuration.

## 5. Database

One `CREATE DATABASE notifications` line in `infra/postgres/init.sh` — an **empty** database. EF
Core migrations create the schema at startup, in every environment; `init.sh` never carries DDL.
Tables live in a `notifications` schema. Columns are snake_case, declared **explicitly** in the EF
configuration rather than by convention.

Locally the postgres image bakes `init.sh`, so the change needs a rebuild:

```bash
docker compose build postgres && docker compose down -v && docker compose up -d
```

## 6. Ports and docs

`NOTIFICATIONS_PORT` (suggested default `5190`) and `MAILPIT_UI_PORT` (default `8025`) in
`.env.example`, and **two** rows in the README port map — the service and Mailpit's UI.

## 7. Its own CLAUDE.md

`notification-service/CLAUDE.md`, pointing back up to the root for platform concerns. Copy the
shape from `events-service/CLAUDE.md`. Three things belong in it that are true of no other service:

- **It holds zero copy and zero domain knowledge**, and what that forbids: no template table, no
  default body, no OPT awareness, and **no email it can send about itself**.
- **The resolution / deliverability seam**: the caller answers *who is this person?*, this service
  answers *can this address receive mail?*
- **The webhook is the platform's only anonymous inbound endpoint from a third party.**

Also write `notification-service/GLOSSARY.md` fixing the vocabulary — batch, recipient, event,
orphan, suppression, `delivery_status` versus `recipient_action`, internal versus external
recipient — and add its row to the root `GLOSSARY-MAP.md`.

## 8. Authentication and the realm

`AddKeycloakAuthentication` plus the service's own `AddAuthorizationPolicies`. The fallback policy
requires an authenticated principal, so a route that forgets `.RequireAuthorization` is still
closed. `/_alive` and `/_ready` stay `AllowAnonymous` — the Dockerfile `HEALTHCHECK` and Compose's
`depends_on: service_healthy` gate on `/_alive`, so a service that authenticated its own health
check would never report healthy.

**The webhook route is the one exception and it is not a Keycloak exception**: it is anonymous and
verifies an ECDSA signature instead. See `04`.

### Realm changes, landing twice

Per ADR-0016: `infra/keycloak/pgti360-realm.json` for realms created later, and a console step in
`docs/deploy-with-dokploy.md` for stage and production, both in the same PR.

| # | Change | Note |
| --- | --- | --- |
| 1 | Audience client `notifications-api` | Matches `events-api`, `crm-api`, `convocatorias-api`. Without it the service validates nothing |
| 2 | Function role `notifications:send` | Gates the write endpoint |
| 3 | Function role `notifications:read` | Gates the history read |
| 4 | `notifications:read` → `central_admin` only | Widening later is a realm edit, no code |
| 5 | `notifications:send` → **no composite, ever** | See the normative line below |
| 6 | `audiencia-notifications-api` mapper on `events-service` | And on `convocatorias-service` when it sends |
| 7 | `notifications:send` on each calling **service account** | `service-account-events-service` first |
| 8 | `view-realm` + `view-users` on `service-account-events-service` | **Not `realm-admin`.** For `06`'s recipient lookup |
| 9 | `audiencia-notifications-api` mapper on `pgti360-app` | Added while building this ticket. Without it a `central_admin` token has the role but not the `aud`, so `05`'s history screen answers **401, not 403**, and the log says nothing. Pulled forward from `05` so the realm is visited once, not twice |

> **Normative: `notifications:send` is never granted to a person.** The endpoint cannot tell a
> person's token from a machine's, so on the fallback policy any logged-in coordinator could send
> mail as MITUR, from MITUR's authenticated domain, with an effect that leaves the platform and
> cannot be retracted.

> **Row 9 was not in the original eight.** It was found by measuring a real token against the running
> service while building this ticket: `pgti360-app` carries an audience mapper for each of the other
> three APIs and had none for this one. It belongs to `05` by subject and to this ticket by cost — a
> realm change lands twice (ADR-0016), and doing it later buys a second console visit for one mapper.

**No code asserts a composite name.** Endpoints check `noun:verb` function roles only, per ADR-0010,
so renaming a job title is a realm edit. The three composite names are provisional until the client
confirms their own titles.

**Two things are deliberately absent.** There is **no** outbound `notification-service` confidential
client (§"Decisions" above), and there is **no** machine-versus-human check in code — Keycloak has
no notion of a machine role, because a service account *is* a user (`service-account-<clientId>`)
with roles hanging off it exactly as they do off a person. Composite discipline is the whole gate.

**Residual risk, stated rather than engineered away:** ADR-0016 gives the console ownership of the
realm after seeding, and no test can see a live realm — a realm-configuration test asserts the JSON
file, which proves nothing about what is running. So `notifications:send` reaching a composite in
the console is a real, undetectable failure. Every function role already carries this exposure; what
differs here is the blast radius, which is why the rule is written in words in ADR-0020.

## 9. Configuration

`Section__Key` → `Section:Key`, environment variables only, never a committed `appsettings`.

**Every variable below lands in `docker-compose.yml`, `docker-compose.override.yml` and
`docker-compose.dokploy.yml`.** The Dokploy file is self-contained — Dokploy does not chain compose
files — and it is what stage and production actually deploy, so a variable added only to the base
file is missing from the one file that ships. `scripts/compose-parity.sh` exists to catch exactly
this and must pass.

| Variable | Local | Stage | Production |
| --- | --- | --- | --- |
| `ConnectionStrings__Postgres` | `Host=postgres;…;Database=notifications` | same host, real password | same |
| `Keycloak__Authority` | `http://keycloak:8080/realms/pgti360` | same | same |
| `Keycloak__Audience` | `notifications-api` | same | same |
| `Keycloak__RequireHttpsMetadata` | `false` | `false` | `false` |
| `Keycloak__AdditionalValidIssuer` | `http://localhost:${KEYCLOAK_PORT}/realms/pgti360` | `https://identidad.${DOMAIN}/realms/pgti360` | same shape |
| `Smtp__Host` | `mailpit` | `mailpit` | `smtp.sendgrid.net` |
| `Smtp__Port` | `1025` | `1025` | `587` |
| `Smtp__RequireTls` | `false` | `false` | `true` |
| `Smtp__Username` | unset | unset | `apikey` (SendGrid's literal fixed username) |
| `Smtp__Password` | unset | unset | the API key, from the secret store |
| `Smtp__AllowExternalRelay` | `false` | `false` | `true` |
| `SendGrid__EventWebhook__PublicKey` | dev key, from `.env` | the real one | the real one |
| `SendGrid__EventWebhook__Enabled` | `true` | `true` | `false` until inbound is confirmed |

`SendGrid__EventWebhook__Enabled` is what stamps `tracking_expected` on every row at insert (`01`
§4), so it is configuration with a permanent trace in the data, not a feature flag.

**There is no `ServiceAuth__*` block and no `Services__*` block.** This service calls nobody.

## 10. Test infrastructure

Two fixtures, mirroring the precedent exactly:

| Fixture | Mirrors | Serves |
| --- | --- | --- |
| `NotificationsApiFactory` | `EventsApiFactory` | the host, `IntegrationTesting` environment |
| `NotificationsApiDbFactory` | `EventsApiDbFactory` / `CrmApiDbFactory` | the same host against a Testcontainers `postgres:17-alpine`, schema applied through EF Core |
| `TestAuthHandler` | the existing one | a principal with a chosen set of roles |
| `FakeSmtpTransport` | `CrmDirectoryStub`, `FileStoreStub` | an in-process SMTP leg that can fail on demand — see `03` |

**`convocatorias-service`'s empty test shells are the named mistake not to repeat.** Every
Definition of Done in this spec set lists assertions rather than an intention.

## Definition of done

- `dotnet build` and `dotnet test` succeed from `notification-service/`, and the `.slnx` lists the
  chassis `.csproj`.
- No `Directory.Build.props` or `Directory.Packages.props` exists under `notification-service/`.
- `docker compose build notification-service` succeeds from the repo root; a bare `docker build`
  from the service folder is not attempted.
- `docker compose build postgres && docker compose down -v && docker compose up -d` leaves an empty
  `notifications` database, and the first migration applies on startup against it.
- `GET /_alive` returns 200 with no token; every other mapped route except the webhook returns 401
  with no token. **Prove the fallback works by adding a route with no `.RequireAuthorization` and
  watching it still return 401.**
- The eight realm changes are in `infra/keycloak/pgti360-realm.json` **and** written up as console
  steps in `docs/deploy-with-dokploy.md`.
- `notifications:send` appears in no composite in the realm JSON, and a test asserts that.
- Every variable in §9 is in all three compose files and `scripts/compose-parity.sh` passes.
- `.env.example` carries `NOTIFICATIONS_PORT` and `MAILPIT_UI_PORT`, and the README port map has
  both rows.
- `notification-service/CLAUDE.md` and `GLOSSARY.md` exist, and `GLOSSARY-MAP.md` links the latter.
- The four fixtures in §10 exist and one smoke test uses each.
