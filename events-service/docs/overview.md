# events-service: overview

> Moved from `events-service/CLAUDE.md` (PGTI-213), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' events-service/docs/*.md` finds it.

## What this service is

The Events module: MITUR promotional events and their execution — roadshow cities, service
suppliers, and per-category supplier quotes with their approval workflow. It owns the `events`
schema and answers on `http://events-service:8080` inside the Compose network. It is the
fully-built reference service; a new service copies its shape (`docs/adding-a-new-service.md`).

## Project structure

```
events-service/                                   # Sorteo360.Events.slnx
├── {Project}.Core/                               # Domain logic, interfaces, DTOs, errors
│   ├── {Feature}/                                # Feature folders (I{Feature}Service + {Feature}Service)
│   ├── Database/                                 # Repository interfaces
│   ├── DTOs/  Mappers/  Cache/
│   └── Errors/                                   # Domain error catalogs only (e.g. QuoteErrors)
├── {Project}.Infrastructure/
│   ├── Database/                                 # EventsDbContext, EventData, RoadshowCityData,
│   │   └── Configurations/                       #   repositories, IEntityTypeConfiguration<T> per entity
│   ├── Directories/                              # StaleTolerantCache and DirectoryRefreshLock
│   ├── Cache/  HealthChecks/  {ExternalClient}/
├── {Project}.Web/
│   ├── Program.cs                                # Composes the chassis (see below)
│   ├── Configuration/DomainServicesConfiguration.cs   # The only file here; Web/Data composition is chassis
│   ├── Auth/                                     # AuthorizationPolicies, CurrentUserAccessor
│   ├── {Feature}Endpoints/                       # Endpoint classes grouped by feature
│   ├── GlobalEndpoints/                          # Service-owned response shapes (PagedResponse,
│   │                                             #   PagingRequest, PagingLimits)
│   ├── Filters/                                  # Service-owned filters (CategoryRouteFilter)
│   ├── Validators/
│   └── OpenApi/                                  # EventsDocumentTransformer, RequiredRoleOperationTransformer
├── {Project}.Tests.Unit/                         # Mirrors Core/Infrastructure (also pure Web helpers like ToProblem)
└── {Project}.Tests.Integration/
    └── Infrastructure/                           # EventsApiFactory, EventsApiDbFactory, TestAuthHandler
```

## Chassis composition

`Program.cs` is the one place that knows **this** service registers a Keycloak scheme, one
policy per role in `AuthorizationPolicies`, `ICurrentUser`, and two OpenAPI transformers; the chassis supplies the plumbing and
never names a service type. That is why it carries a `using` for both
`Sorteo360.Platform.Configuration` and `Sorteo360.Events.Web.Configuration` — expected,
not a leftover.

Calls to other services go through the chassis `AddServiceClient` (`Sorteo360.Platform.Http`):
it signs each request with this service's own realm identity — `ServiceAuth__ClientId` /
`__ClientSecret`, wired in `docker-compose.yml` — and applies the resilience pipeline. The typed
client, its routes and its wire DTOs stay here, in `Infrastructure/`. The destinations are
`Services__Crm__BaseUrl` (`Infrastructure/Crm/`: the OPT and company directories and the raffle's
mailability), `Services__Notifications__BaseUrl`, `Services__Keycloak__BaseUrl` and
`Services__Convocatorias__BaseUrl` (`Infrastructure/Convocatorias/`: the raffle's check-in list).

FirmaGOB is the exception (ADR-0035). The MITUR middleware is not a platform service: it knows
nothing of our realm and takes its own per-platform API key (`X-Api-Key`), so `IFirmagobGateway`
gets a plain typed `HttpClient` (`Web/Configuration/FirmagobGatewayConfiguration.cs`) and never
`AddServiceClient`. The chassis pipeline is also wrong for it: it retries at once, inside one call.
Only the download retries over HTTP, through its own small Polly pipeline in `FirmagobHttpGateway`.
A create carries the SignatureRequest id as its `Idempotency-Key`, so `SignatureDrainService` sends
it again on a later poll whenever it did not land or may have landed, and the middleware answers a
repeat with the request it already made. `Firmagob__Transport` picks `stub` (in memory, serves the
sent PDF back once) or `http`, and has no default in code; with `http`, `Firmagob__BaseUrl`,
`__ApiKey`, `__CallbackApiKey` and `__CallbackSigningSecret` are validated at startup.

`ISignatureService.SendAsync` refuses what the middleware cannot take before a byte is stored
(`SignatureInputRules`, PGTI-312): first failure wins, each with its own `400` code in
`SignatureErrors`. The limits are `Firmagob__MaxDocuments` (10), `__MaxDocumentBytes` (20 MB) and
`__MaxRequestBytes` (25 MB), none of which can be raised: the first two are the middleware's own
caps, the third ours. A
document is a PDF by its bytes (the chassis `FileSignatures.Detect`), never its name. A cédula
must be `000-0000000-0` and never appears in an error message; the middleware's free-text
`message` goes through `Cedula.MaskWithin` before it is logged.

This service is also the one that switches on the chassis token relay (`AddTokenRelay` /
`MapTokenRelay`, ADR-0037): `POST /v1/auth/token` for an External System that cannot speak OAuth2,
off while `TokenRelay__AllowedClientIds` is empty. `AddProxyHeaders(1)` and `UseForwardedHeaders`
first in the pipeline are what let its per-IP limit see the caller instead of Traefik.

## Domain-shaped code stays here

Domain-shaped things stay here: `QuoteErrors` in `Core/Errors/`, `PagedResponse` in
`Web/GlobalEndpoints/` (alongside `PagingRequest`, `PagingLimits` and `ToResponse`, which every
paged listing shares — a new one inherits the request, inherits `PagingRequestValidator<T>`, and
maps with `ToResponse`, so the page size ceiling is the service's and not whatever each route
file remembered), `CategoryRouteFilter` in `Web/Filters/`, `AuthorizationPolicies` and the
identity accessors in `Web/Auth/`, and `DomainServicesConfiguration`.

## Domain naming — this service was migrated

Unlike its siblings, this service was **built Spanish and migrated in full** to English under
ADR-0008 (backlog card "English domain naming migration"), so nothing here should carry a Spanish
identifier. Concretely: entities `Event`, `ServiceSupplier`, `Quote`, `Candidate`,
`RoadshowCity`; schema `events` with tables `event`, `service_supplier`, `quote`, `candidate`,
`candidate_proposal`, `proposal_attachment`, `event_image`, `quote_decision`, `roadshow_city`; routes `/service-suppliers`,
`/candidates`, `/submit`, `/close`; error codes like `EVENT_NOT_FOUND`.

The Spanish rendering of each term for prose lives in `GLOSSARY.md`, whose glossary is bilingual —
its `_Avoid_` lists apply to identifiers only. The general rule is in
`docs/dotnet-service-conventions.md`, §Naming.

> `frontend/` finished this migration: its routes and error codes are English and
> aligned. Its http mode is broken against this service for an unrelated reason — it still
> addresses a candidate where the API names a proposal, and reads a price off the candidate that
> no longer carries one. See `frontend/CLAUDE.md`, §Contract debt.

## Database specifics

> The table model, the platform table conventions and Money are in `docs/ARCHITECTURE.md` §4;
> this section keeps the reasons that file does not give.

Beyond the platform EF Core rules:

- `EventsDbContext`; persistence classes `EventData` and `RoadshowCityData`
- **A Candidate's prices and its recommendation live in `candidate_proposal`, one row per
  offer.** `quote_text`, the setup and teardown dates, both amounts and `is_recommended` moved off
  `candidate`, and a candidate carries one or more — a venue quoting each of its salones. The API
  returns them as the candidate's `proposals` (oldest first, never empty), which is why the
  candidate itself shows nothing priced. The row
  repeats its candidate's `quote_id` so `ux_candidate_proposal_final_amount` stays scoped to the
  quote, as it was when the column lived on `candidate`: at most one live proposal per quote
  carries a final amount, and it is the approved one. The repository still clears any other in the
  same transaction, because the index is checked per statement and rows written before approval
  named an offer could carry one. Removing the last proposal of a
  candidate is refused (`CANDIDATE_LAST_PROPOSAL`); removing the candidate soft-deletes all of
  them. `ux_candidate_proposal_recommended` is scoped the same way, so **recommending one offer
  clears the previous one wherever it sat** — another of the same candidate's salones, or another
  supplier's — in one transaction. `Candidate.IsRecommended` is computed from the proposals rather
  than stored, and the JSON keeps it so a screen can highlight the supplier's card. **Approving
  names a proposal too** — `quote_decision.winner_proposal_id` — because a venue quoting three
  salones gets contracted for one, and the winning candidate is read off that row rather than
  stored or returned beside it. The final amount then lands on the approved proposal and nowhere
  else, so the approved offer and the contracted offer are the same row; moving it to another of
  the winner's offers has no API path, which is the `approved-winner-falls-through` gap. **The
  attachments came down too** — `proposal_attachment.candidate_proposal_id` — so three salones are
  three prices with three written cotizaciones, each one filed against the offer it backs. The cross-event list
  (`GET /v1/quotes`) reads two amounts off those rows, and they answer different questions:
  `estimatedAmountRecommended`, what is being asked for approval, and `estimatedAmountHighest`,
  the priciest offer in the quote and so the ceiling of what the category could cost — the number
  that exists while nothing is recommended yet. The ceiling comes from `Money.Highest`, which
  answers `null` when the offers mix currencies: there is no exchange rate anywhere in this
  service, which is also why `TotalCostCalculator` refuses a mixed event instead of converting.
  Each row also carries `lastOutcome`, the outcome of the latest attempt (null when never
  submitted or still pending, and the only thing telling a rejected draft from a withdrawn one),
  and `winnerFinalAmount`, the final amount of the proposal the approval named. Both come off the
  same latest-attempt join and proposal rows, so a screen can tell whether a category blocks the
  event's close from one list call instead of opening every quote's detail.
- **A supplier occupies one candidate slot per quote**, backed by
  `ux_candidate_quote_service_supplier` on `(quote_id, service_supplier_id) WHERE deleted_at IS
  NULL`. The filter is what lets a removed candidate's supplier be added again rather than
  colliding with its tombstone; the index is the backstop for two simultaneous adds, which the
  chassis reports as `DB_CONFLICT`. The migration that created it merged pre-existing duplicates by
  repointing their proposals onto the oldest candidate of the supplier — no price was dropped,
  since a candidate carrying several proposals is the correct shape.
- **`event.visibility` has no database default, and `event.language` no longer takes null.** The
  default for visibility lives at the HTTP edge, where a create without one becomes `private`; a
  row inserted around the API has to say which it wants, because publishing by accident does not
  undo. `ck_event_visibility` is the backstop, matching `ck_event_status`. The migration that
  added them backfilled every existing row to `private` and every null language to `es`. **A patch
  that omits `visibility` keeps the stored value** — the one field on that endpoint that omitting
  does not clear, and `UpdateEventDto.Visibility` is nullable for exactly that reason. That is a
  named exception to ADR-0009 rule 1, recorded **in the ADR**, which is where a second one has to
  go too; do not carve another out here. A patch that
  omits `language` is `400 EVENT_LANGUAGE_REQUIRED`, raised in the handler rather than the
  validator because the chassis validation filter answers `ValidationProblem`, which carries no
  `code`.
- **`event.seating_layout` is nullable, with no default, and follows ADR-0009 with no
  exception** (PGTI-350). Null means nobody has chosen a layout, which is not `Cocktail`, so
  existing rows were left null. A patch that omits it clears it, unlike `visibility`. An unknown
  value is a field-level 400 with no code; `ck_event_seating_layout` is the backstop.
- **Optimistic concurrency uses the PostgreSQL system column `xmin`** as the token
  (`builder.Property<uint>("xmin").IsRowVersion()`) on `event`, `quote`, `raffle` and `signature_request`, so lifecycle races
  (close vs cancel) surface as 409 rather than lost updates
- The `xmin` token generates **no DDL**: remove any scaffolded `AddColumn`/`DropColumn` for `xmin`
  from a new migration
- **An event edit goes through `Event.Update`, and the repository copies only what an edit owns.**
  `EventService.UpdateAsync` reads the event without tracking, `Event.Update` applies the edit's
  rules (not terminal, a positive budget, ADR-0009's replace with the `visibility` exception), and
  `EventRepository.UpdateAsync` reads the row again, with tracking, to save it. `xmin` covers only
  that second read up to `SaveChanges`, not the window between the two reads. Two things close the
  part of that window that matters. The repository refuses with `409 EVENT_NOT_EDITABLE` when the
  second read finds the row closed or cancelled, so an edit that raced a cancel or close never
  lands. And it never writes `Status`, `TotalCost` or `CancellationReason`, so a start that ran in
  the window survives the edit. What stays open is two edits of the same fields racing each other:
  the last one wins, as before. Closing that too, with a token on `Event` or the ADR-0041 row lock,
  is left to the seating ticket, whose seat caps depend on rows outside `event` that `xmin` cannot
  protect anyway.

## Authorization specifics

The platform role model is in `docs/dotnet-service-conventions.md`, §Authentication. Every role
this service checks, what it guards and which job composite grants it is in the platform's one
catalogue, `docs/roles.md`; `scripts/roles-parity.sh` fails when it, the realm JSON and
`AuthorizationPolicies` disagree.

`Keycloak__Audience` here is `events-api`. Integration tests use `TestAuthHandler` with
`X-Test-Anonymous`, `X-Test-Roles`, and `X-Test-Sub`.

## API documentation specifics

Two transformers, both this service's, passed in from `Program.cs`: `EventsDocumentTransformer`
(document title/description) and `RequiredRoleOperationTransformer` (renders each endpoint's
required role from its authorization policy, so descriptions never restate it; an
`AllowAnonymous` route gets an "anonymous" line instead, as the token relay does).

`OpenApiDocumentTests` (integration) asserts presence and structure on `/openapi/v1.json` — a new
endpoint without a summary **fails the build**. It never asserts on prose text, so wording changes
are free.

Reads that carry a `.WithDescription()` because their rules are not self-evident: `ListEvents`,
`GetEventById`, `ListQuotes`, `GetQuote`, the supplier list / `pool` / `history`,
`ListServiceSupplierPhotos`, `ListProposalAttachments` and `ListEventImages` (the link id and
the file id are not interchangeable), `GetServiceSupplierById` (its photos carry that same pair of ids, and its `company` needs the same
nullable-name and header explanation the event reads carry), and
`GetFileById` / `GetFileContent` (the `missing` flag and the 404-vs-503 split
need explaining). `UploadProposalAttachment` and `RemoveProposalAttachment` carry one too, since
the flat route and the unlink-not-delete semantics are not self-evident, and so do
`UploadEventImage` and `RemoveEventImage`, where the one-cover replace rule has to be stated
somewhere a client will read it. The two event
reads share `EventEndpointsDescriptions.CrmDirectoryStatusDescription`, and that one plus the
supplier detail both build on `Filters.CrmDirectoryStatusDescription.Ladder` — a nullable
CRM-resolved name and the `X-Crm-Directory-Status` header need explaining wherever they surface, and
once is enough.
