# ADR-0012 — Cross-service scaffolding lives in a shared source project, not a NuGet package

- Status: Accepted
- Date: 2026-08-02

## Context

Every .NET service in this monorepo is scaffolded from the same template, and the template's
cross-cutting plumbing was copied along with it. The same twelve files exist once per service in
`events-service`, `crm-service`, and `convocatorias-service`: the error model (`BaseError`,
`ErrorKind`), the persistence-failure translator (`DbResult`), the endpoint plumbing (`IEndpoint`,
`EndpointExtensions`, `BaseErrorExtensions`), the validation filter and its route helper, the health
endpoints, `Program.cs`, and the two host-composition classes.

Copies drift, and these did. `DbResult` carries roughly seventy lines of real policy — SQLSTATE
23505/23503 to 409, `DbUpdateConcurrencyException` to 409, generic Spanish messages so that
constraint names never reach a client. One copy has an extra overload for preconditions checked
inside the transaction; one is an earlier generation with no logger parameter, no SQLSTATE
translation, and an exception message forwarded straight to the caller. `ErrorKind` is missing its
`Forbidden` member in one service, so that service's `ToProblem` has no 403 branch. The route helper
declares its 400 in the OpenAPI document in one service and not in the others. Three copies of the
same security-relevant policy, two of them under test.

At least nine services are planned. Every service scaffolded from a copy is another divergence, and
the divergences are precisely in the code that decides what a client is told when persistence fails.

Two ways to share it were considered: publish the chassis as a NuGet package, or keep it as a source
project referenced from the repo. There is no artifact store to publish to, versioning a package adds
a release step between writing a fix and consuming it, and the root `CLAUDE.md` already mandates that
a cross-service contract change lands in a single PR across producers and consumers — which is the
same lockstep a package's version pinning is designed to avoid.

## Decision

- **The chassis is a shared source project**, `shared/Sorteo360.Platform`, referenced by services
  through `ProjectReference`. It has its own unit-test project, `shared/Sorteo360.Platform.Tests.Unit`.
  No NuGet package, no artifact store, no independent version. One team, one repo, one build.
- **NuGet is reconsidered if and only if services leave the monorepo.** A service that ships from its
  own repository cannot use a `ProjectReference`, and at that point the packaging cost buys something.
  Until then it buys a release step.
- **The chassis carries technical scaffolding only** — the error model, persistence result
  translation, endpoint plumbing, health endpoints, validation, and the host composition
  (`AddWebServices`, `AddDataServices<TContext>`, `AddKeycloakAuthentication`). **Never domain
  types, DTOs, or business rules.** The test of whether something belongs is whether it would read
  identically in a service whose domain nobody has thought of yet.
    - Host composition passes that test only because the parts that differ per service are
      parameters, not branches: the `DbContext` is a type argument, the OpenAPI transformers arrive
      as a callback, and authentication is a **separate extension a service calls or does not**.
      `convocatorias-service` protects nothing today, so it calls neither
      `AddKeycloakAuthentication` nor `AddAuthorizationPolicies` and needs no `Keycloak__*` env
      vars. A flag parameter would have made "no auth" a configuration of auth; a second call site
      keeps it an absence.
- **`DomainServicesConfiguration` is explicitly out of scope and stays per-service, permanently.** It
  registers a service's own application services, its typed HTTP clients and its settings objects;
  it is a list of that service's domain, spelled as configuration. It shared a folder with the two
  host-composition classes that do belong in the chassis, which made it the obvious thing for a
  later reader to sweep up while "finishing the job". Now that those two have moved, it is the only
  file left in `Web/Configuration/`, and a lone file in a folder reads like a leftover. It is not an
  oversight.
- **Cross-service domain contracts are a separate, future decision.** Shared DTOs and event contracts
  between services belong under a `shared/contracts` location decided when async messaging lands (see
  the "Async messaging" section of `docs/adding-a-new-service.md`) — not in the chassis, which must stay
  ignorant of every domain.
- **Where copies had drifted, the `events-service` generation is canonical.** It is the richest, the
  best documented, and the only one that never forwards database detail to a client. Adopting it is a
  behaviour change for the services that carried an older copy — a 403 branch and an OpenAPI 400
  declaration they lacked, and generic messages replacing leaked ones — and that is the point.
- **The chassis has its own solution at the repo root**, `Sorteo360.Platform.slnx`, so that "what
  the chassis is" stays answerable without opening a service. A consuming service also lists
  `Sorteo360.Platform.csproj` in its own `.slnx`, next to the `ProjectReference` that pulls it in:
  the service solution is the unit a developer opens and the unit `dotnet build`/`dotnet test` runs,
  and a solution that omits a project it compiles hides that dependency from both. The service's test
  projects are not repeated in the platform solution, nor the platform's in a service solution, so no
  suite runs twice.
    - This is not the constraint it would have been if a service's Docker build context were still its
      own folder — a solution naming `shared/` would then have stated a dependency the container build
      could not resolve. The context moves to the repo root for exactly the same reason (see
      Consequences), which settles both questions the same way.
- **Central package management and the common build properties live at the repo root.**
  `Directory.Packages.props` (the union of the service files) and `Directory.Build.props` sit beside
  the shared projects, because MSBuild walks up from a project directory and stops at the first file
  it finds. A service keeps its own copies until it adopts the root pair; there is no ambiguity in
  either state, only in an intent to have both.

## Consequences

- A service consuming the chassis must build from a Docker context that contains `shared/` — in
  Compose, `build.context: .` with `dockerfile: <service>/Dockerfile`, the service's Dockerfile
  copying `shared/` alongside its own folder, and a root `.dockerignore`. This is the structural cost
  of the decision, and it is per-service and one-time.
- The chassis carries a single namespace root, `Sorteo360.Platform.*`, so every consuming service
  takes a `using` sweep across the files that referenced its own copy. No variant of this extraction
  avoids that churn, which is why it should precede a service's domain work rather than follow it.
- The service copies of these types are duplicates until each service adopts the chassis and deletes
  its own. A copy that stays is a copy that drifts again; adopting the chassis is not optional work
  for a service that already has one.
- A fix to the chassis reaches every service at the next build, with no release step — and equally,
  a regression reaches every service at the next build. The chassis's own test project is what makes
  that trade acceptable, and it is the reason the `DbResult` suites of the individual services
  consolidate there rather than being deleted with the copies.
- The root `Directory.Packages.props` is the union of what the services pin, not the intersection: a
  service that does not reference a package simply ignores its entry. A central `PackageVersion` is
  inert until something references it — it declares a version, not a dependency — so the union costs
  nothing and spares the next service an edit.
