# events-service — Service Instructions

The Events & Raffles module: events lifecycle, participant imports & webhooks, check-in sync,
file storage, live prize draws, handovers and notarial PDFs, in the `events` database;
`http://events-service:8080` inside Compose. `docs/dotnet-service-conventions.md` applies in full
and `shared/CLAUDE.md` describes the chassis. `Core/`, `Infrastructure/`, `Web/` and `Tests.*`
live under `events-service/`.

```bash
dotnet build events-service/Sorteo360.Events.slnx
dotnet test  events-service/Sorteo360.Events.slnx
dotnet ef migrations add <Name> \
  --project events-service/Sorteo360.Events.Infrastructure \
  --startup-project events-service/Sorteo360.Events.Web \
  --output-dir Database/Migrations
```

## Need X, read Y

| Need | Read |
| --- | --- |
| The layer layout, chassis wiring, naming, database specifics, roles, OpenAPI | `events-service/docs/overview.md` |
| The stale-tolerant cache, CRM degradation ladder (when CRM is enabled) | `events-service/docs/directories.md` |
| Stored files, prize sponsor logos, event images | `events-service/docs/files.md` |
| The raffle's seams, roster imports, webhooks, mailability, handovers, PDF records | `events-service/docs/raffles.md` |
| The tables, the route list and the state machines | `docs/ARCHITECTURE.md` §4 to §6 |
| Every app role: what it does, which job and service accounts hold it | `docs/roles.md` |
| What a term means | `events-service/GLOSSARY.md` |

## Structure and naming

- `Program.cs` wires Keycloak authentication (`sorteo360` realm), `AuthorizationPolicies`,
  `ICurrentUser`, and OpenAPI transformers (`EventsDocumentTransformer`, `RequiredRoleOperationTransformer`).
- Domain-shaped code stays here: `PagedResponse`, `AuthorizationPolicies`, `DomainServicesConfiguration`.
- A new paged listing inherits `PagingRequest` and `PagingRequestValidator<T>` and maps with `ToResponse`.
- Function roles reach users through job composites (`docs/roles.md`); `Keycloak__Audience` is `events-api`;
  tests use `TestAuthHandler` with `X-Test-Anonymous`, `X-Test-Roles`, `X-Test-Sub`.
- Feature endpoints are documented in OpenAPI (`OpenApiDocumentTests` verifies summaries, 401, access lines, tags).

## Database

- `xmin` is the concurrency token on `event` and `raffle`; delete any scaffolded `AddColumn` / `DropColumn`
  for `xmin` from a new migration.
- `event.visibility` has no database default; the HTTP edge defaults it to `private`.
- `event.opt_office_id` is nullable when operating without a CRM directory; when omitted, an organizer label is used.
- An event edit goes through `Event.Update`; `EventRepository.UpdateAsync` copies editable fields only.

## Raffles

- `IAttendanceSource` is `RosterAttendanceSource`: the raffle's own roster in Postgres, filtered by
  `requiresCheckIn` (only checked-in rows when `true`, the whole roster when `false`).
- Draft reads use `IRecentAttendance` (cached copy); `start` and `SetOverrideAsync` read attendance live.
- Qualification runs through `Qualification.Classify`, first match wins.
- Voiding a prize draw excludes the winner from the live eligible pool unless explicitly reinstated via
  `POST /v1/events/{eventId}/raffle/attendees/{attendeeId}/reinstate` (`RafflesWrite`).
- Winner emails go through `IRaffleWinnerNotifier` → `notification-service` after commit, in the background
  (`RaffleWinnerNotificationWorker`, enqueued by the `complete` endpoint); email failures never
  fail completion.
- Notarial PDFs render via MigraDoc (`MigraDocNotarialRecordRenderer`), listing winners, redemptions, voids,
  reinstatements, and operator names resolved via `KeycloakRaffleOperatorDirectory`.

## Tests

- Substitute neighbours with WireMock stubs (`CrmDirectoryStub`, `NotificationStub`, `KeycloakRealmStub`).
- `CrmDirectoryStub` / `KeycloakRealmStub` serve the test realm `sorteo360`.
- Scale benchmarks (25,000 participants / 10,000 prizes, A31) are excluded from normal CI and run explicitly with:
  `dotnet test events-service/Sorteo360.Events.Tests.Integration --filter Category=Scale`
