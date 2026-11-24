# .NET service conventions

Platform-wide conventions for every .NET service in this monorepo (`events-service`,
`crm-service`, `convocatorias-service`, `notification-service`). A service's own `CLAUDE.md`
covers its domain, its structure, and its **deviations** from this file; anything not
contradicted there applies as written here.

Related: root `CLAUDE.md` (cross-service rules), `shared/CLAUDE.md` (the chassis).

## Tech stack

- **Framework:** .NET 10 (ASP.NET Core Minimal APIs)
- **Language:** C# with nullable reference types and implicit usings enabled
- **IDE:** NeoVim
- **Package management:** Central Package Management via the **repo-root**
  `Directory.Packages.props` / `Directory.Build.props`. No service keeps copies (MSBuild walks
  up until it finds them)
- **Shared chassis:** technical scaffolding comes from `shared/Sorteo360.Platform` by
  `ProjectReference` — see `shared/CLAUDE.md`

### Core libraries

| Purpose           | Package                                                                                                           |
| ----------------- | ----------------------------------------------------------------------------------------------------------------- |
| ORM / Data Access | Microsoft.EntityFrameworkCore + Npgsql.EntityFrameworkCore.PostgreSQL                                             |
| Database          | Npgsql (PostgreSQL)                                                                                               |
| Validation        | FluentValidation                                                                                                  |
| Result Pattern    | CSharpFunctionalExtensions                                                                                        |
| Authentication    | Microsoft.AspNetCore.Authentication.JwtBearer (Keycloak) — only in services that protect endpoints                |
| Logging           | Serilog (AspNetCore, Enrichers.Environment, Enrichers.Process, Exceptions, Expressions, Sinks.Async)              |
| Caching           | Microsoft.Extensions.Caching.Abstractions (`IDistributedCache`, in-memory backend)                                 |
| API Versioning    | Asp.Versioning.Http + Asp.Versioning.Mvc.ApiExplorer                                                              |
| API Documentation | Microsoft.AspNetCore.OpenApi (native .NET 10 OpenAPI doc) + Scalar.AspNetCore                                     |
| Health Checks     | AspNetCore.HealthChecks.UI.Client, AspNetCore.HealthChecks.NpgSql                                                 |
| Resilience        | Polly.Core                                                                                                        |
| HTTP Client       | Standard HttpClientFactory (no Refit)                                                                             |

### Testing libraries

| Purpose               | Package                                                  |
| --------------------- | -------------------------------------------------------- |
| Test Framework        | xUnit                                                    |
| Mocking               | NSubstitute                                              |
| Assertions            | Shouldly                                                 |
| Integration Test Host | Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) |
| Containers            | Testcontainers / Testcontainers.PostgreSql               |
| HTTP Mocking          | WireMock.Net                                             |
| Coverage              | coverlet.collector / coverlet.msbuild                    |

## Coding conventions

### General

- File-scoped namespaces
- `var` when the type is obvious from the right-hand side
- Prefer pattern matching (`is`, `is not`, property patterns)
- Raw string literals (triple-quote `"""`) for multi-line strings
- Standard constructors only — **do not use primary constructors**, except input DTOs (below)
- Guard clauses with `ArgumentNullException.ThrowIfNull()` in constructors (domain entities and
  services; **not** input DTOs)
- Constants as `private const` inside the class that uses them
- Settings classes expose a `public static string SectionName`/`ConfigurationName` for binding
- Prefer `record` for immutable data (DTOs, error types, settings)
- `CancellationToken` on every async method that touches I/O
- Mark configuration classes `[ExcludeFromCodeCoverage]`
- Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

### Naming: English domain names (ADR-0008)

**English — everything the type system and the wire see:** entity and type names, business
properties, DB tables and columns (snake_case, declared explicitly via `ToTable`/
`HasColumnName` — never left to EF conventions), DTO/JSON fields, route segments including
action verbs, error codes, and OpenAPI tags/operation IDs. **One exception**, recorded in ADR-0008:
the Dominican cédula is `cedula` (`Cedula`, `cedula_sealed`, `cedulaMasked`), never `nationalId`.
Do not add another Spanish identifier without superseding that ADR.

**Spanish — prose humans read, not the type system:** code comments, log messages, the
client-facing `ProblemDetails` message text, and the OpenAPI `summary`/`description` text that
`/scalar` renders. The end-user UI is Spanish, translated at the presentation layer; the API
never ships translated field names.

**Stored codes are frozen:** lifecycle/status values are English machine codes (`planning`,
`draft`, `submitted`); business classification values keep the client's words (`Ambientacion`,
`Roadshow`, `single_provider`). The UI translates codes; the database does not. This supersedes
the ADR-0006 hybrid convention. See `docs/adr/0008-nombres-de-dominio-en-ingles.md`.

### Input DTOs vs domain entities (ADR-0001)

The "no primary constructors / explicit null-guards" rules are scoped by **role**, not by
`record`-vs-`class` (both are records):

- **Input DTOs** — service-input types under `Core/DTOs`, validated by a FluentValidation
  validator at the Web boundary — are **terse positional records** with **no** null-guards. They
  sit between two guards: the validator at the door and the domain entity's constructor in Core.
  The DTO's own guard would be the redundant one in the middle.
- **Domain entities** keep explicit constructors with `ArgumentNullException.ThrowIfNull`
  guards — they defend their invariants independent of any caller.

This is the one scoped exception to "do not use primary constructors". See
`docs/adr/0001-input-dtos-are-terse-records.md`.

### Result pattern (CSharpFunctionalExtensions)

- Service methods return `Result<T>` or `Result<T, TError>` — never throw for business-logic
  failures
- `Result.Success(value)` / `Result.Failure<T>("message")` explicitly
- `UnitResult<TError>` for void operations that can fail
- Check `.IsSuccess` / `.IsFailure` before touching `.Value`
- Infrastructure errors are captured in `BaseError` (or a derived type)

## Minimal API endpoints

- One endpoint class per feature implementing `IEndpoint` (static abstract interface), requiring
  `static abstract void DefineEndpoints(IEndpointRouteBuilder app)` and
  `static abstract void AddServices(IServiceCollection services, IConfiguration configuration)`
- Endpoints are auto-discovered by assembly scanning in the chassis `EndpointExtensions`
- Route groups via `.MapGroup()` for shared prefixes; authorization via
  `.RequireAuthorization(policyName)`
- Inject with `[FromServices]`, `[FromRoute]`, `[FromQuery]`
- Validate inputs at the endpoint with FluentValidation validators
- Errors reach HTTP through the chassis `BaseErrorExtensions.ToProblem()` — the single
  `ErrorKind` → status table: `NotFound` → 404, `Invalid` → 400, `Forbidden` → 403,
  `Conflict` → 409, `Unavailable` → 503, `Unauthenticated` → 401, `TooManyRequests` → 429,
  anything else → 500
- `.WithValidation<TRequest>()` (chassis) runs the validation filter **and** chains
  `.ProducesValidationProblem()`, so the 400 is declared exactly where the filter is. Because
  that is explicit response metadata the inferred `200` disappears — declare success codes
  yourself

### PATCH replaces the editable fields (ADR-0009)

A `PATCH` body carries the **complete editable representation**, not a set of changes: the
repository copies every field onto the row, so an omitted *optional* field is **cleared**, not
preserved. Callers read, modify, and write the whole representation back.

**One field is carved out**, recorded in ADR-0009 itself: `event.visibility` is *preserved* when
omitted, because it governs visibility to the public internet and the event form has no control
for it yet, so clearing it would unpublish an advertised event on every unrelated edit. **Sealed
values are the second**, recorded in ADR-0031 (decision 11): a cédula, passport, visa or bank account
number omitted from a `PATCH`, or from an item a list-replacing `PUT` sends back with its `id`,
keeps the stored one, because the screen only knows it masked. Do not
add another exception in a service file — take it to the ADR.

The rule that follows: **no property of an update request may take an implicit default.** Every
value type in an `Update*Request` — `bool`, `int`, `decimal`, `Guid`, `DateTime`, enums — is
declared **nullable** and made **required by its validator**, so an omitted field is a 400 rather
than a guessed value. Reference types get this for free. `Create*Request` types are exempt:
creation has no prior state to destroy.

## Validation

- FluentValidation for all input validation (request payloads, settings objects)
- Settings validated at startup to fail fast. A registration extension that binds a section
  itself (`AddKeycloakAuthentication`, `AddServiceClient`) throws `InvalidOperationException`
  naming the missing key; a settings object validated by FluentValidation throws
  `ValidationException`
- `ValidateDataAnnotations().ValidateOnStart()` with the `IOptions<T>` pattern for simple settings

## Database access (EF Core)

- The service's `DbContext` (scoped) is the single entry point for all database access, and is
  itself the unit of work
- Each service owns one schema, declared explicitly per entity
  (`ToTable("<table>", "<schema>")`) with snake_case columns via `HasColumnName`
- Map to **internal persistence classes** (`{Entity}Data`) — never expose EF entities to Core or
  Web; repositories convert to and from domain entities manually
- Entity configuration lives in `Configurations/` as `IEntityTypeConfiguration<T>`; the context's
  `OnModelCreating` discovers them via `ApplyConfigurationsFromAssembly`
- Soft delete is a **global query filter** (`HasQueryFilter(e => e.DeletedAt == null)`) on every
  entity with a `deleted_at` column, so queries exclude deleted rows with no `WHERE` in the
  repository
- Read methods use `.AsNoTracking()` wherever they do not mutate
- Write methods call `SaveChangesAsync` inside `DbResult.TryAsync(logger, action, …)` (chassis) —
  never let EF exceptions bubble to callers. Mapping and message policy are in `shared/CLAUDE.md`
- Audit timestamps are maintained **in code**: every write path sets `UpdatedAt` explicitly.
  There are no database triggers
- Schema evolution is **EF Core migrations** (`dotnet ef migrations add <Name>`), the single
  schema authority; pending migrations are applied at startup (`Database.MigrateAsync()`, skipped
  in `IntegrationTesting`, where the Testcontainers factory applies them). Raw SQL goes in
  `migrationBuilder.Sql(...)`; one-time seed data uses `migrationBuilder.InsertData`, never EF
  `HasData` (which re-asserts values on later model changes). A seed into a catalogue an
  administrator already edits by API uses `Sql` with `INSERT ... ON CONFLICT DO NOTHING`
  instead, so a row created by hand with the same code or name does not stop startup
- `IQueryable<T>` composition is the intended approach for filtering: build a base query from the
  `DbSet` and layer `.Where()` / `.OrderBy()` / `.Skip()` / `.Take()` conditionally before
  `ToListAsync`, so one round-trip serves every filter combination

## Caching

- `IDistributedCache`, registered via `AddDistributedMemoryCache()` — single-instance deployment
  today, no distributed backend is wired (`docs/adr/0007-redis-caching-deferred.md`)
- Cache service interfaces live in Core, implementations in Infrastructure

## Authentication & authorization

Applies to services that protect endpoints; it is **opt-in** (see `shared/CLAUDE.md`). Role
names and policies are domain, so they live in the service, never the chassis.

- **Keycloak** (`docs/adr/0004-identidad-keycloak-on-prem.md`, on-prem) via standard JWT bearer
  validation, wired by the chassis `AddKeycloakAuthentication`. Config comes from the `Keycloak`
  section env vars: `Keycloak__Authority` (realm URL, also main issuer), `Keycloak__Audience`
  (the service's client-audiencia), `Keycloak__RequireHttpsMetadata` (off only in local HTTP),
  `Keycloak__AdditionalValidIssuer` (the public issuer — localhost in dev, `KEYCLOAK_HOSTNAME` in
  stage/prod)
- `MapInboundClaims = false`: claims keep their token names — user id in `sub`, name in
  `preferred_username`, roles in the flat multivalued `roles` claim, all emitted by mappers on the
  `pgti360-app` client
- **Two-layer role model:** endpoints check fine-grained *function* roles; users are assigned a
  *composite* role in Keycloak that expands to them inside the token. Composites are granted **per
  job, not per service**, so one composite carries a person's permissions across the whole
  platform. Changing who holds which function is admin-console work, not code; renaming a
  composite is a realm-file edit (`docs/adr/0010-un-rol-compuesto-por-puesto.md`)
- Policies live in a static `AuthorizationPolicies` class in `Web/Auth/`, one policy per function
  role (policy name == role name). A **fallback policy** covers every endpoint without an explicit
  policy: it requires an authenticated user holding `platform:staff` or `platform:service`, so
  reads need no function role but do need one of those two (`docs/adr/0025`). The two constants
  live in `AuthorizationPolicies` outside `All`. Health endpoints and the API docs are
  `AllowAnonymous`
- Prefer `.RequireAuthorization(...)` **per route** when a feature's group mixes policy-less reads
  with role-gated writes; a group-level policy would silently gate the reads too. The group still
  owns what genuinely applies to every route in it — the version set, the tag, the 401/500
  responses
- The current user (`ICurrentUser` in Core, `CurrentUserAccessor` in `Web/Auth/`) resolves the
  `sub` claim and is stamped as `created_by_id`/`updated_by_id` on every write — never
  `Guid.Empty`
- Integration tests bypass real auth with `TestAuthHandler` (test scheme, registered as default by
  the factories): a full-role user plus `platform:staff` by default, with `X-Test-*` headers to
  simulate anonymous requests, restricted roles, or a specific actor. A test that restricts roles and
  hits a route on the fallback adds `platform:staff`, or `platform:service` for a service caller
- Keycloak config is not required when `environment.IsEnvironment("IntegrationTesting")` — the
  test scheme replaces it

## Logging

- Serilog configured from `appsettings.json` via `ReadFrom.Configuration()`
- `UseSerilogRequestLogging()` with custom level logic (health-check noise down to Debug)
- Enrich with Environment, Process, Exceptions
- Structured logging — pass values as parameters, never string interpolation
- **Log messages are Spanish**

## Health checks

- The routes — `/_alive`, `/_ready`, `/_health`, `/_health/all` — are defined by the chassis
  `HealthEndpoints` and arrive through endpoint discovery. **Do not redefine them in a service**
- What a service owns is the **checks behind them**: the chassis `AddDataServices<TContext>`
  registers them and the tags decide which route reports them
- Tags: `internal`, `external`, `critical`, `database`, `cache`. `/_ready` reports `ready`;
  `/_health` reports `critical` + `internal`; `/_health/all` reports everything
- All health endpoints are `AllowAnonymous` and excluded from the OpenAPI document

## API versioning

- URL segment versioning: `v{version:apiVersion}/...`, default version 1, assumed when
  unspecified, via `UrlSegmentApiVersionReader`
- The ApiExplorer `GroupNameFormat = "'v'VVV"` yields one group name (`v1`) per discovered
  version; register one OpenAPI document per version with `AddOpenApi("v{n}")`

## API documentation

- .NET 10 **native OpenAPI** (`Microsoft.AspNetCore.OpenApi`) — no Swashbuckle/Swagger
- The chassis `AddWebServices` calls `AddOpenApi("v1", ...)`; document transformers belong to the
  service (`Web/OpenApi/`) and are passed in from `Program.cs`
- `Program.cs` maps the document at `/openapi/{documentName}.json` and the **Scalar** UI at
  `/scalar`, both `.AllowAnonymous()` — the fallback policy would block them otherwise
- Docs are on in non-Production environments; in Production they are off unless temporarily
  enabled with `ApiDocs__Enabled=true`
- **`/scalar` is the authority on endpoint rules** — it is generated from the code that enforces
  them. `docs/ARCHITECTURE.md` and any request collection are subordinate and may lag

### Describing operations

- **Every operation carries `.WithSummary()`.** `.WithDescription()` goes on every write and state
  transition; a read gets one **only when its filters, required params, or eligibility rules are
  not self-evident**. A read whose summary already says everything gets none — padding is worse
  than silence
- Description text lives in a co-located `{Feature}EndpointsDescriptions.cs` (`internal static
  class` of `const string` raw literals) beside the endpoint class, never inline in
  `DefineEndpoints`, which stays a scannable route table. Do not name these files after Scalar:
  Scalar is the renderer, the text is OpenAPI metadata
- Register is **impersonal** Spanish — no second person, no voseo/tuteo. English identifiers stay
  English inside the Spanish prose, in backticks
- Descriptions never restate the **required role** and never enumerate **status codes**: roles are
  rendered from the endpoint's authorization policy by a transformer, status codes come from
  `.Produces`/`.ProducesProblem`. Anything a rule depends on is machine-rendered so it cannot
  drift
- Every route declares its success and problem responses. A code goes on the `MapGroup` only when
  it truly applies to the whole group — `401` and `500` always do; `403` goes per route wherever
  the group mixes policy-less reads with gated writes, so the document never claims a 403 that
  cannot happen

## Security

- Remove the `Server` header:
  `builder.WebHost.UseKestrel(options => options.AddServerHeader = false)`
- All non-health endpoints require authentication by default

## Comments

- Comments are **Spanish**, but English words are fine where they are the real term (don't write
  "clase" for `class`)
- Interface files are documented with XML documentation comments
- Private methods carry XML documentation comments

Every comment, from a class summary to a `//` line inside a method, is written for a teammate who
knows the business but has never opened this file and did not see the session that wrote it.

- Fix the name first. A comment says only what a good name cannot: why the code exists, a unit, a
  limit, or what happens at the edge.
- Write full sentences, each with a subject and a verb. Every "lo", "la", "esa" or "la siguiente"
  names its noun.
- Use the words the code and the `GLOSSARY.md` use. Never coin a word during a session, such as
  "vuelta" for one run of a worker.
- A ticket or ADR number may follow the reason. It never replaces it.
- `<summary>` is one sentence. Anything more goes in `<remarks>`.

Before, a comment only its author could read:

```csharp
/// <summary>Tope por clase de documento y por vuelta; lo que sobre sale en la siguiente.</summary>
private const int BatchSize = 100;
```

After:

```csharp
/// <summary>Máximo de documentos de cada tipo que borra una ejecución del worker.</summary>
/// <remarks>
/// Son tres tipos: escaneos reemplazados, pasaportes quitados y visas quitadas. Por eso una
/// ejecución borra hasta 300. Si quedan más, se borran en la próxima ejecución.
/// </remarks>
private const int MaxDocumentsPerKindPerRun = 100;
```

## Containerization

Platform-level orchestration is in the root `CLAUDE.md`; operations are in `README.md`. Per
service:

- Ships as a container built from `<service>/Dockerfile` (multi-stage: `sdk:10.0` builds and
  publishes → `aspnet:10.0` runtime)
- **The build context is the monorepo root**, not the service folder, because the chassis
  `ProjectReference` points outside a per-folder context. Compose declares `context: .` with
  `dockerfile: <service>/Dockerfile`, every path inside the Dockerfile is written from the root,
  and only `shared/` plus the service's own folder are COPYed. The root `.dockerignore` keeps the
  context small; services have none of their own
- Build with `docker compose build <service>` — a bare `docker build` from the service folder no
  longer has the chassis in scope
- Kestrel listens on **8080** inside the container (`ASPNETCORE_HTTP_PORTS=8080`); the host port
  is mapped by Compose, never hardcoded
- The image runs as a **non-root** user (`$APP_UID` from the base image). Do not add steps that
  need root at runtime
- All runtime config comes from environment variables (conventions in the root `CLAUDE.md`), never
  baked into the image or a committed `appsettings`
- The container `HEALTHCHECK` and Compose readiness gating hit `/_alive` and `/_ready`; these must
  exist and stay `AllowAnonymous`

### Service-to-service communication

- **Synchronous:** typed `HttpClientFactory` clients with Polly resilience, registered through
  the chassis `AddServiceClient<TClient, TImpl>(configuration, serviceName)` — never a bare
  `HttpClient` and never a hand-rolled resilience pipeline. Base URLs come from
  `Services__{ServiceName}__BaseUrl` env vars pointing at Compose DNS names (e.g.
  `http://events-service:8080`) — never IPs or `localhost`
- **The caller authenticates as itself:** the chassis attaches a client-credentials token
  obtained with `ServiceAuth__ClientId` / `__ClientSecret` from the realm in
  `Keycloak__Authority`. The typed client owns the routes, the wire DTOs and the translation of
  the response; a dependency that is down becomes `ErrorKind.Unavailable` → 503, never a 500.
  See `shared/CLAUDE.md`
- **Asynchronous:** nothing exists yet. See `docs/adding-a-new-service.md`, §Async messaging

## Patterns NOT used — do not suggest these

- **MediatR / CQRS middleware** — services are called directly from endpoints
- **AutoMapper** — manual mapping methods or extension methods
- **FluentAssertions** — use Shouldly (FluentAssertions changed their license)
- **Moq** — use NSubstitute (Moq had a security incident)
- **Bogus / AutoFixture** — create test data manually with helper methods
- **Minimal API source generators** — use the reflection-based `IEndpoint` pattern
- **Carter** — use the `IEndpoint` pattern
- **Controllers / MVC** — Minimal APIs exclusively
- **A separate Unit of Work abstraction** — the `DbContext` *is* the unit of work; repositories
  wrap it and call `SaveChangesAsync`. Do not add an `IUnitOfWork` layer on top
- **Global exception handlers returning Problem Details** — errors are handled explicitly per
  endpoint via the Result pattern

## Testing

### Unit tests

- Mirror the source project's folder structure
- Name test classes `{ClassUnderTest}Tests`; naming convention for methods is
  `MethodName_Returns_When`
- Use the constructor for setup (substitutes + SUT), `Substitute.For<T>()` for every dependency,
  and Shouldly for assertions
- `[Fact]` for single cases, `[Theory]` + `[InlineData]` for parameterized ones
- Helper factory methods for test objects; group related tests with `#region`
- The Infrastructure project declares `InternalsVisibleTo` so unit tests can reach its internal
  persistence classes

### Integration tests

- `WebApplicationFactory<IApiMarker>`, where `IApiMarker` is an empty interface in the Web project
- A custom factory overrides `ConfigureWebHost` to set the environment to `"IntegrationTesting"`,
  replace real services with fakes via `ConfigureTestServices`, register the test authentication
  handler, and use `AddDistributedMemoryCache()` (same as the running service)
- Where a real database is needed, a second factory spins a `Testcontainers.PostgreSql` container
  and applies the **same EF migrations** the service applies at boot
- Test classes implement `IClassFixture<TFactory>`; tests make HTTP calls against the real
  pipeline (routing, auth, serialization)
- `WireMock.Net` mocks external HTTP dependencies

### Anti-patterns to avoid

- Do not use `HttpClient` directly — always the factory's `CreateClient()`
- Do not test implementation details — test behaviour through the public API
- Do not share mutable state between tests

## Common commands

Run from the service folder. Substitute the service's own project names.

```bash
dotnet restore
dotnet build
dotnet run  --project {Project}.Web
dotnet test                             # everything in the solution
dotnet test {Project}.Tests.Unit
dotnet test {Project}.Tests.Integration
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# The chassis has its own suite, deliberately not part of any service solution:
dotnet test ../Sorteo360.Platform.slnx

# Add a migration (the schema's single authority):
dotnet ef migrations add <Name> \
  --project {Project}.Infrastructure \
  --startup-project {Project}.Web \
  --output-dir Database/Migrations
```

## Workflow: creating or modifying API endpoints

1. Plan the change first — new/updated methods, paths, request payloads
2. Confirm the proposed changes with the user
3. Implement
