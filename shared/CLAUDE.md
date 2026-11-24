# shared/Sorteo360.Platform — the platform chassis

> Chassis-level guide. For cross-service rules and the repo map see the root `CLAUDE.md`;
> for a service's internals see that service's own `CLAUDE.md`.

The chassis is the technical scaffolding every .NET service used to carry its own copy of.
It now lives here once and is consumed by `ProjectReference` from each service's `Core`,
`Infrastructure` and `Web` projects (`docs/adr/0012-shared-platform-chassis.md`).

## What it carries

| Namespace                                | Types                                                    |
| ---------------------------------------- | -------------------------------------------------------- |
| `Sorteo360.Platform.Errors`          | `BaseError`, `ErrorKind`                                 |
| `Sorteo360.Platform.Database`        | `DbResult`                                               |
| `Sorteo360.Platform.GlobalEndpoints` | `IEndpoint`, `EndpointExtensions`, `BaseErrorExtensions`  |
| `Sorteo360.Platform.Filters`         | `ValidationFilter`, `ValidationFilterExtensions`          |
| `Sorteo360.Platform.HealthEndpoints` | `HealthEndpoints` (`/_alive`, `/_ready`, `/_health`)      |
| `Sorteo360.Platform.Configuration`   | `AddWebServices`, `AddDataServices<TContext>`             |
| `Sorteo360.Platform.Auth`            | `AddKeycloakAuthentication`, `KeycloakSettings`           |
| `Sorteo360.Platform.Http`            | `AddServiceClient`, `ServiceAuthSettings`, `IServiceTokenProvider`, `AddProxyHeaders` |
| `Sorteo360.Platform.Files`           | `IFileStore`, `FilesystemFileStore`, `AddFileStore`, `StoredFileLocation`, `FileContentInspector`, `FileSignature`, `FileSignatures`, `FileRejection` |

## The one rule

**Technical scaffolding only — never domain types, DTOs, or business rules.** Those stay in the
service, and so does `DomainServicesConfiguration`. Role names, error catalogs, response shapes
like `PagedResponse`, and service-owned filters are domain: they never move here. Shared
*domain* contracts between services are a separate, undecided question (see
`docs/adding-a-new-service.md`, §Async messaging), not a chassis concern.

The inverse rule is the services': **do not re-create a chassis type under a service
namespace** — reference it.

## Behaviour worth knowing before you change it

- **Endpoint discovery always scans the chassis.** `AddEndpoints`/`MapEndpoints` take
  `params Assembly[]` and add the chassis assembly to whatever the caller passes,
  de-duplicated. So a service's `Program.cs` calls each **once** with
  `Assembly.GetExecutingAssembly()` and the health endpoints arrive unnamed. That is
  deliberate: `/_alive` is what the container `HEALTHCHECK` and Compose's
  `depends_on: service_healthy` gate on, and a service that forgot to scan the chassis used to
  lose it silently.
- **Authentication is opt-in.** `AddWebServices` registers Serilog, API versioning and the
  OpenAPI document only. A service that protects endpoints calls `AddKeycloakAuthentication`
  itself, plus its own `AuthorizationPolicies`; one that protects nothing calls neither and
  needs no `Keycloak__*` env vars (`convocatorias-service` is the live example).
- **`DbResult` is `public`**, so it needs no `InternalsVisibleTo` to be testable. It maps by
  nature: `DbUpdateConcurrencyException` and PostgreSQL SQLSTATE `23505` (unique) / `23503`
  (foreign key) → `ErrorKind.Conflict` (409); everything else → `Internal` (500). Client-facing
  messages are generic Spanish; the raw exception goes only to the log. `TryResultAsync` is the
  variant for an operation that builds its own `Result<T, BaseError>` because it checks a
  precondition inside the same transaction.
- **`ErrorKind` → HTTP lives in one place**, `BaseErrorExtensions.ToProblem()`: `NotFound` → 404,
  `Invalid` → 400, `Forbidden` → 403, `Conflict` → 409, `Unavailable` → 503, `Unauthenticated` →
  401, `TooManyRequests` → 429, anything else → 500.
  `Invalid` is for a well-formed request whose *content* breaks a rule the edge validator could not
  check — a file whose bytes are not what it claims. `Conflict` stays for what the resource's own
  state forbids. It also emits
  `BaseError.Code` as a **`code` extension** on the ProblemDetails, omitting the key when the error
  carries none. That is what lets a client tell apart two failures sharing a status — two 409s for
  different reasons — without parsing the message, which is Spanish prose written for a person.
- **A payload rides along through `BaseError.ProblemExtensions`.** A subclass overrides that
  dictionary and `ToProblem` merges it beside the `code`, so a failure can carry the datum that
  resolves it — the contact already holding an email, for one — without the endpoint assembling a
  ProblemDetails by hand and without the chassis knowing the service's type. It defaults to empty,
  so an error that overrides nothing responds exactly as it did before the seam existed. The `code`
  key is the mapper's: a contributed `code` is dropped, never merged.
- **Outbound calls know how to authenticate and how to survive a network — nothing else.**
  `AddServiceClient<TClient, TImpl>(configuration, serviceName)` builds a typed `HttpClient`
  whose base address is `Services:{serviceName}:BaseUrl`, signs every request with the
  service's own client-credentials token, and wraps the call in one Polly pipeline per
  destination (total timeout, retry with jitter, circuit breaker, per-attempt timeout). It
  never sees a route, a DTO or which service is on the other end: those stay in the caller's
  Infrastructure layer, which is also where the response is turned into a `Result`.
  - The identity is `ServiceAuth__ClientId` / `__ClientSecret` — **one section, not one per
    callee**, because a service has a single identity however many services it calls. The
    token endpoint is derived from `Keycloak__Authority`; there is no extra variable.
  - The token is cached in-process until 30 s before it expires, and a 401 from the
    destination triggers exactly one retry with a forced refresh (a token can be invalidated
    before it expires; a second 401 is a misconfiguration that retrying will not fix). A 403
    is never retried.
  - Registration **fails at startup** when the base URL or the identity is missing, so a
    misconfigured dependency never reaches a user's first request.
  - Everything Polly throws leaves as `HttpRequestException`, including a failure to obtain
    the token (`ServiceAuthenticationException` derives from it). A caller catches that one
    type and returns `ErrorKind.Unavailable` → **503**, which is what that kind exists for:
    this service is healthy, its dependency is not.
  - **A token failure is exempt from the destination's retry and circuit breaker**, which is
    why the pipeline predicate excludes `ServiceAuthenticationException`. Without that, a
    Keycloak outage would be retried against the callee and counted as its failures, opening
    the circuit on a service that never misbehaved.
- **`AddProxyHeaders(proxyHopCount)` is how a service sees the caller's real IP behind Traefik.**
  The service passes how many proxies stand in front of it, one per proxy, and calls
  `app.UseForwardedHeaders()` first in its pipeline. Who may forward is not a parameter: only private networks are trusted, so
  `X-Forwarded-For` from a public address is ignored. The hop count is a limit, not "all", so a
  caller who writes the header by hand cannot push a made-up IP past the real proxy.
- **The token relay is in the chassis, switched on in events-service only** (ADR-0037). It is not
  an `IEndpoint` on purpose: endpoint discovery always scans the chassis, so it would appear in
  every service. A service opts in with `AddTokenRelay(configuration)` plus `MapTokenRelay()`, and
  `TokenRelay:AllowedClientIds` empty keeps it off: the route answers a plain `404` (an unmapped
  route would answer an anonymous caller `401` through the fallback policy). The Keycloak call is
  a named `HttpClient`, never `AddServiceClient`, which would attach the service's own token. The
  two rate limits (per allowed client id, per caller IP) are counted inside the handler with a
  chained `PartitionedRateLimiter`, not with `UseRateLimiter`, because the client id is in the
  body; so the service adds nothing to its pipeline beyond `UseForwardedHeaders`.
- **The file store knows how to hold bytes and nothing else.** `IFileStore` saves, opens and
  deletes by an **opaque storage key** the caller stores and hands back without ever
  interpreting it. No signature names a provider, a remote path or a share id, which is what
  lets the provider change without a business rule moving (`docs/adr/0014`). What a file is
  *for* — and the tables that record it — stay in the service.
  - `AddFileStore(configuration)` picks the provider from **`FileStore:Provider`**, never from
    `ASPNETCORE_ENVIRONMENT`, so any environment can point at any provider. Missing or unknown
    configuration **fails at startup**, like `AddServiceClient`.
  - `FilesystemFileStore` is a real second implementation, not a test double: local development
    and integration tests run on it, and because it has no HTTP and no remote ids it is what
    proves the seam is abstract rather than shaped like one vendor.
  - **Failures are exceptions, not `Result`** — same stance as the typed clients: they are
    infrastructure faults, not business rules. `FileNotFoundInStoreException` (the store
    answered and the file is gone) and `FileStoreUnavailableException` (the store did not
    answer) are **never** conflated; the caller maps them to its own codes. The `FileStoreException`
    base is left uncaught on purpose — it means a corrupt key, which is this service's bug.
- **What a file *is* is decided here, once; what a service answers about it is not.**
  `FileContentInspector.InspectAsync` counts the size against a limit the caller passes
  (`DefaultMaxSizeBytes` is the agreed 25 MB), reads the first bytes through `FileSignatures`
  and returns the detected `FileSignature` or a `FileRejection` reason: `TooLarge`,
  `TypeNotAllowed`, `HeicNotSupported` (an iPhone's default, recognised only so it can be refused
  with a reason) or `TypeMismatch` (the declared type or extension contradicts the bytes; declaring
  nothing never does). The stream must be seekable and is left where it arrived, because the caller
  hands it to `IFileStore` next. A caller may pass its own accepted list to widen one upload kind
  without widening the rest: `FileSignatures.Xlsx` is in no default list, and crm-service adds it
  for company files only. An .xlsx starts like any ZIP, so it is recognised inside the package
  (`SpreadsheetPackage`): `[Content_Types].xml` must declare `xl/workbook.xml` with the plain
  workbook type, which refuses a .docx, a plain ZIP and a macro workbook (.xlsm). The package is
  opened only when that caller accepts workbooks and the header is a ZIP's. A reason, not a `BaseError`: the error code, the Spanish message,
  per-owner quotas and an "images only" narrowing (`FileSignatures.AcceptedImages`) stay in the
  service, as events-service's `UploadPolicy` shows.
- **`.WithValidation<TRequest>()` runs the filter and chains `.ProducesValidationProblem()`.**
  The two go together on purpose, so the 400 is declared exactly where the filter is. Because
  that is explicit response metadata, the inferred `200` disappears — callers declare their own
  success codes.

## Changing the chassis

A change here reaches **all three services at once**. Treat it as a cross-service contract
change: build and test every consumer in the same PR.

```bash
# The chassis has its own solution and suite, deliberately NOT in any service solution
# (so no service runs it twice):
dotnet test Sorteo360.Platform.slnx      # from the repo root

# Then the consumers:
dotnet test events-service/Sorteo360.Events.slnx
dotnet test crm-service/Sorteo360.Crm.slnx
dotnet test convocatorias-service/Sorteo360.Convocatorias.slnx
```

Chassis behaviour is covered here (`shared/Sorteo360.Platform.Tests.Unit`), not in a
service's suite. Coding conventions are the platform's — see
`docs/dotnet-service-conventions.md`, §Coding conventions; they apply unchanged to chassis code.

One deliberate deviation from the platform testing conventions: outbound HTTP is covered with
the in-suite `FakeHttpMessageHandler`, not WireMock.Net. WireMock mocks a *server*, and what
needs asserting here is the **handler chain** — which token went on the wire, how many attempts
the pipeline made — with no host to point it at. WireMock stays the tool for a service's
integration tests.
