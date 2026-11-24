# Foundation — Spec

Everything the service needs before it has a domain model: an identity, a fail-closed authorization
default, two route prefixes, a way to reach the internet that is not a public hostname, and the test
infrastructure every later spec asserts through.

Nothing here is convocatorias-specific enough to be interesting on its own, and every later spec
breaks without it.

## Decisions behind this shape

- The service authenticates nothing today. `Program.cs` deliberately skips the chassis
  `AddKeycloakAuthentication`, which was correct for an empty service and stops being correct with
  the first endpoint.
- Stage has been serving that empty service on a public hostname with Scalar and
  `/openapi/v1.json` open in every non-Production environment. Removing the hostname is part of this
  spec, not a follow-up.
- events-service is the reference vertical for structure. When this spec and that code disagree on
  *structure*, the code wins; on *behaviour*, the spec wins.

## 1. Authentication and the fail-closed default

`Program.cs` calls the chassis `AddKeycloakAuthentication(builder.Configuration,
builder.Environment)`, then `app.UseAuthentication()` and `app.UseAuthorization()`.

A `Web/Auth/AuthorizationPolicies.cs`, copied in shape from
`events-service/.../Auth/AuthorizationPolicies.cs`:

- **A fallback policy requiring an authenticated principal.** Every endpoint is protected unless it
  says otherwise, which is the only ordering that fails closed.
- Named policies for the four roles in §2, each checking one role claim the way
  `AuthorizationPolicies.QuotesApprove` checks `quotes:approve`.

`/_alive`, `/_ready` and `/_health` stay `AllowAnonymous`, per the root `CLAUDE.md`. So does
everything under the public prefix, and nothing else ever does.

Role names are domain, so they stay in this service and never move to the chassis.

## 2. The realm

Two clients and four roles, none of which exist today. Per ADR-0016 each lands **twice**: in
`infra/keycloak/pgti360-realm.json` for environments created later, and as a console step in
`docs/deploy-with-dokploy.md` for stage, which is already running.

**Clients**, mirroring `events-api` / `events-service`:

| Client | Kind | Carries |
| ------ | ---- | ------- |
| `convocatorias-api` | audience | the audience the service validates tokens against |
| `convocatorias-service` | confidential, service accounts enabled | the caller identity |

The `convocatorias-service` service account holds `crm:service-read` and `contacts:write` as
**directly-assigned realm roles**, exactly as `service-account-events-service` holds
`crm:service-read` today, plus an `audiencia-events-api` protocol mapper so it can read events.

**Roles**, and the composites they join:

| Role | Added to | Guards |
| ---- | -------- | ------ |
| `campaigns:write` | `opt_coordinator`, `opt_director`, `central_admin` | build a draft, select recipients, upload, reconcile |
| `campaigns:launch` | `opt_coordinator`, `opt_director`, `central_admin` | launch a campaign |
| `convocatorias:read` | `opt_coordinator`, `opt_director`, `central_admin` | every tracking read |

**Three roles, not four.** The client removed the approval step on 2026-09-07, so `campaigns:submit`
and `campaigns:approve` were never built (ADR-0017, amended). There is no approver and no
self-decision guard.

**`campaigns:launch` is separate from `campaigns:write` even though the same three composites hold
both**, so nobody is narrowed on day one. It is split because narrowing later has to stay a console
change: the day the client decides only dirección may launch, that is dropping one role from one
composite and shipping nothing. Folding launch into `campaigns:write` would make the same decision a
code change plus a realm change plus a deploy, which is exactly the trade ADR-0010's
fine-grained/composite split exists to avoid.

No persona role is invented. `opt_director` is MITUR's dirección, seeded as `direccion`, and it
already holds `quotes:approve`, `events:cancel` and `events:close`.

`convocatorias:read` is service-wide rather than per resource. A single tracking screen crosses
campaigns, registrations and reconciliation, so `campaigns:read` would be a name that lies about its
scope, and three read roles produce a set nobody can assign correctly. `crm:service-read` is the
precedent.

## 3. Route prefixes

Exactly two, both versioned:

- `v{version:apiVersion}/public/...` — anonymous, rate limited, §5.
- `v{version:apiVersion}/convocatorias/...` — everything internal.

One prefix per service, not crm-service's one per resource. §4 explains why the count matters.

`/v1/events/{id}/participants` is not available: `frontend/nginx.conf` has `location /v1/` as a
catch-all to events-service, so that path would be swallowed whole, and peeling off
`location /v1/events` would hand events-service its own resource to us.

## 4. How the service reaches the internet

**Through the frontend's nginx, and by no other path.**

1. Add two `location` blocks to `frontend/nginx.conf`, `/v1/public/` and `/v1/convocatorias/`, each
   `proxy_pass`ing to `http://convocatorias-service:8080` through the declared resolver, following
   the existing crm-service blocks verbatim. nginx picks the longest prefix, so both win over the
   `/v1/` catch-all.
2. Add the same two prefixes to the twin list in `frontend/vite.config.ts`. **Nothing compares the
   two files.** A prefix added to one and not the other lands silently in events-service, and the
   config's own comment already warns about this.
3. **Delete the `pgti360-convocatorias` Traefik labels from `docker-compose.dokploy.yml`.** The
   service loses its hostname.

The consequence is the point: `/scalar` and `/openapi/v1.json` stop being reachable from the
internet, because nginx never proxies them. They stay available inside the Compose network. This is
a network-level guarantee in one file, and it removes a public hostname rather than adding one.

A dedicated public-facing proxy container was rejected: same property, plus a fourth container, an
image to patch and a second point of failure.

## 5. The public/internal split, enforced in process

An integration test walks `EndpointDataSource` and asserts that **every mapped endpoint either sits
under the public prefix or carries an authorization requirement**, with `/_alive`, `/_ready` and
`/_health*` as the named exception list.

Endpoint discovery here is assembly scanning and each `IEndpoint` maps its own routes with no
structural guard, so without this test "public endpoints live apart" is a convention that one
misplaced `.AllowAnonymous()` breaks silently.

The test is worth writing in this spec even though it proves nothing until §1 lands, because the two
belong to the same PR.

## 6. Rate limiting

The built-in ASP.NET rate limiter, three axes on the public prefix:

| Axis | Applies to | Catches |
| ---- | ---------- | ------- |
| Per IP, fixed window | the whole public prefix | ordinary flooding |
| Per token | resolve, unsubscribe | enumeration that rotates IPs |
| Per email | registration submit | using the portal to mail-bomb a victim |

`429` carries `Retry-After` and a machine `code`, so the portal renders a real message rather than a
generic failure.

**`ForwardedHeaders` lands in the same PR or the per-IP axis is decorative.** Traefik and nginx both
terminate and forward, so without `KnownNetworks` set to the Docker network every request arrives
from one container IP and the limit degrades into a global one. There are two proxy hops to account
for.

Counters live in process memory, consistent with ADR-0007. One container runs, so per-instance is
accurate, and a redeploy resetting counters is a short window nobody is watching for. Redis was
rejected on the merits: it shares counters across instances, which is not a problem we have, and
ASP.NET's limiter has no distributed provider anyway. Revisit only if this service ever runs more
than one container.

**No package pin is needed.** `AddRateLimiter` and `AddFixedWindowLimiter` compile from the shared
framework on .NET 10; verified by building a probe on 2026-09-03. The root
`Directory.Packages.props` is untouched by this spec.

**No CORS policy anywhere.** Every call is same-origin through nginx.

## 7. Packages

Three pins, all in the **root** `Directory.Packages.props` and referenced from the service. A
service must never keep its own copy.

| Package | Version | Licence | For |
| ------- | ------- | ------- | --- |
| `MiniExcel` | 1.46.0 | Apache-2.0 | xlsx parsing |
| `Sep` | 0.17.0 | MIT | csv parsing |
| `Net.Codecrete.QrCodeGenerator` | 3.1.0 | MIT | QR generation |

All three have **zero native dependencies**, so the Dockerfile gains no `apt` line and
`System.Drawing.Common` stays out of the image.

**EPPlus is disqualified** from 5.0.3 onward: its Polyform Noncommercial licence has a government
carve-out that arguably covers MITUR, while EPPlus's own FAQ says the licence follows whoever directs
the developers. That is a paid legal question, not a technical one, and not worth answering when a
free option wins on merit. **NPOI carries the same trap without advertising it**: the repository is
Apache-2.0 and the nupkg ships a maintenance-fee EULA.

Memory was measured, not assumed, in a .NET 10 Linux container against a 200k-row 8 MB workbook:
MiniExcel peaked at 3 MB, ClosedXML at 189 MB, OpenXML DOM at 482 MB. Streaming is not a premature
optimisation here.

## 8. Startup

- **`CodePagesEncodingProvider.Instance` is registered at startup**, or `05`'s Windows-1252 fallback
  throws `NotSupportedException` on .NET 10 Linux. `Encoding.GetEncoding(28591)` works without it,
  which makes the wrong fix look right, and ISO-8859-1 and Windows-1252 differ exactly where a
  Spanish-locale Excel export puts its curly quotes.
- **`MigrateAsync()` on startup**, copying the events-service block. EF Core migrations are the
  single schema authority in every environment.
- Encoding registration and migration both run before the first request is served.

## 9. Configuration

Every variable lands in `docker-compose.yml` **and** `docker-compose.dokploy.yml`. The Dokploy file
is self-contained, inherits nothing, and is what stage and production actually deploy; a variable
added only to the base compose leaves the service crash-looping on deploy while every local check
stays green. `scripts/compose-parity.sh` compares keys in CI, so the keys must match even where the
values differ.

| Variable | Notes |
| -------- | ----- |
| `ConnectionStrings__Postgres` | exists |
| `Keycloak__Authority`, `__Audience`, `__AdditionalValidIssuer` | new, §1 |
| `Services__Crm__BaseUrl` | new |
| `Services__Events__BaseUrl` | new |
| `Services__Notifications__BaseUrl` | new, only read when the transport is `http` |
| `ServiceAuth__ClientId`, `ServiceAuth__ClientSecret` | new, the `convocatorias-service` client |
| `Notifications__Transport` | new, `stub` or `http`, **required with no default**. See `04` |
| `Portal__BaseUrl` | new, the `app.${DOMAIN}` origin the invitation links are built from |

`.env.example` and the README port map are updated in the same PR.

Every outbound call goes through the chassis `AddServiceClient`, never a bare `HttpClient`, so each
one carries this service's own client-credentials token and the shared resilience pipeline.

## 10. Test infrastructure

**One seam: the service's own HTTP API.** Integration tests start the real application through
`WebApplicationFactory<IApiMarker>` against a real Postgres, with the three neighbours replaced by
WireMock servers. Unit tests cover only Core logic that has no I/O.

The neighbour is substituted, not the typed client, for the reason
`events-service/.../Infrastructure/CrmDirectoryStub.cs` already states in its own comment: what has
to be exercised is the whole chain, the client, the chassis resilience pipeline, the cache and the
degradation, and none of that runs if the interface is replaced by a double.

| Fixture | Mirrors | Serves |
| ------- | ------- | ------ |
| `ConvocatoriasApiFactory` | `EventsApiFactory` | the host, `IntegrationTesting` environment |
| `ConvocatoriasApiDbFactory` | `EventsApiDbFactory` | the same host against a **Testcontainers** `postgres:17-alpine`, schema applied through EF Core |
| `CrmStub` | `CrmDirectoryStub` | contact list and exact lookup, create with its `409`, mailability, opt-out |
| `EventsStub` | same shape | event detail and the filtered list |
| `NotificationStub` | same shape | `POST /v1/notifications` returning the per-recipient `202` |
| `TestAuthHandler` | the existing one | a principal with a chosen set of roles |

**Tests drive the `http` transport against `NotificationStub`**, not the stub gateway, for the same
reason. `StubCampaignSendGateway` gets its own tests as the production component it is.

Each stub must be able to serve a failure on demand: a timeout, a `503`, and enough consecutive
failures to open the chassis circuit breaker, because `05`'s confirmation loop depends on that
breaker opening.

## Definition of done

- `AddKeycloakAuthentication` is called, the fallback policy requires an authenticated principal, and
  a request with no token to any non-public, non-health route gets `401`.
- The four roles and two clients are in `infra/keycloak/pgti360-realm.json` **and** written up as
  console steps in `docs/deploy-with-dokploy.md`.
- `frontend/nginx.conf` and `frontend/vite.config.ts` both carry `/v1/public/` and
  `/v1/convocatorias/`.
- The `pgti360-convocatorias` Traefik labels are gone from `docker-compose.dokploy.yml`, and
  `convocatorias.${DOMAIN}` no longer resolves to anything.
- The `EndpointDataSource` test passes and fails when a route is given `.AllowAnonymous()` outside
  the public prefix. Prove it fails by trying it once.
- `ForwardedHeaders` is configured and a request through two proxy hops is rate limited by its real
  client IP.
- Every variable in §9 is in both compose files and `scripts/compose-parity.sh` passes.
- The first migration applies on startup against an empty `convocatorias` database.
- The six fixtures in §10 exist and one smoke test uses each.
