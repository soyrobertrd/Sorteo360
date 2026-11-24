# ADR-0008 — English domain names platform-wide

- Status: Accepted
- Date: 2026-07-25 (amended 2026-07-28: client mandate recorded, migration scope
  widened; amended 2026-07-29: API documentation prose ruled Spanish; amended
  2026-08-12: the demo-SPA exemption moved from `frontend/` to
  `frontend-sandbox/`; amended 2026-10-08: `frontend-sandbox/` deleted, the
  exemption returned to `frontend/`; amended 2026-10-17: `cedula` is the one
  exception)
- Supersedes: ADR-0006 (hybrid naming: Spanish domain language, English scaffolding)

## Context

ADR-0006 drew the naming line by role: Spanish for anything the client would say
in a meeting (`Evento`, `Proveedor`, `/someter`), English for technical
scaffolding (`Id`, `CreatedAt`, `Status`). It worked while `eventos-service` was
the only domain in the repo, but the split has costs that grow with the platform:

- Every new name is a judgment call about whether the client "would say it",
  and the boundary is genuinely fuzzy for words like `Contact`, `Company`, or
  `Office` that exist in both vocabularies.
- Compound names read as neither language (`FechaInicio` + `CreatedAt` in one
  entity, `Empresa.Country`), and the mixed forms leak into DB columns, JSON
  fields, and route segments where they are expensive to change later.
- Everything the platform integrates with — .NET, EF Core, PostgreSQL idiom,
  HTTP conventions, tooling, and the wider hiring pool — is English.

The CRM effort is the first time a second domain is modeled from scratch, which
is the cheapest possible moment to settle this.

What settled it for good (recorded 2026-07-28): the client's own requirement is
that everything in the code be English except comments — partly because Spanish
special characters (accents, eñes) complicate identifiers. The original hybrid
convention rested on a misreading of that requirement; English domain names are
a client mandate, not a stylistic preference.

## Decision

**Domain names are English across the platform.** This covers entity and type
names, properties, database tables and columns, DTO/JSON fields, and route
segments — `Company`, `Contact`, `OptOffice`, `/v1/companies`, `opt_office_id`.
There is no longer a role-based split to reason about; the scaffolding rules of
ADR-0006 stand simply because they were already English.

Unchanged, and deliberately so:

- **Code comments are Spanish.** **Log messages are Spanish.** These are read by
  the team, not by the type system.
- **The end-user UI is Spanish.** That is a presentation concern — labels are
  translated at the edge, not stored.
- **API documentation prose is Spanish** (amended 2026-07-29). OpenAPI
  `summary`, `description`, and tag descriptions — what `/scalar` renders — are
  prose the type system never sees, exactly like comments and log messages.
  Everything the contract is made of stays English: tags, operation IDs, routes,
  schemas, and field names. A Spanish sentence therefore carries English
  identifiers in backticks (`exceptionReason`, `draft`), and that mixture is
  correct rather than unfortunate — the payload really is English.
- **Stored codes already in production are frozen.** Lifecycle codes stay as
  they are (`planning`, `draft`, `submitted`); business classification values
  keep the client's own words (`Ambientacion`, `Traductores`, `Roadshow`).
  Renaming stored values is a data migration, not a naming decision.

## Consequences

- **`crm-service` is built English from the start** — no rename is ever needed
  there. This is not a CRM-only exception; it is the platform rule, applied first
  where it costs nothing.
- **`eventos-service` migrates separately** (backlog: "English domain naming
  migration", fully specified 2026-07-28; it may interleave with the CRM
  effort). The scope goes beyond the domain surface: infrastructure identifiers
  rename too (folder and compose service `events-service`, namespaces
  `Sorteo360.Events.*`, database `events`), the Keycloak client and the
  Spanish permission roles rename together with the authorization policies that
  mirror them, and the Bruno collection (contents and file names) and OpenAPI
  tags/operation IDs go English. With nothing deployed, the existing migrations
  are regenerated into a single English `InitialCreate` rather than renamed in
  place. Until it lands, `eventos-service/CLAUDE.md` continues to describe the
  hybrid rule as the accurate account of that codebase.
- **`frontend/` is exempt from the same-PR contract rule.** It is the
  platform's official frontend and speaks the English contract, so the exemption
  covers timing only: a contract change may leave it behind, and it catches up in
  its own backlog card (root `CLAUDE.md`). When this ADR was written, `frontend/`
  was expected to be renamed `frontend-sandbox` and brought onto the English
  contract later. Instead it adopted the English contract on its own branch.
  From 2026-08-12 the exemption sat on `frontend-sandbox/`, a frozen mock-only
  copy that kept the client demo up while `frontend/` reached stage. That copy
  spoke the pre-ADR Spanish contract and was deleted rather than migrated (see
  `.scratch/frontend-sandbox-split/PRD.md`). On 2026-10-08 (PGTI-5) it was
  deleted and the exemption returned to `frontend/`.
- **`convocatorias-service` keeps its Spanish identifiers for now.** It is a
  bare scaffold whose English context name is unknowable until its domain is
  modeled; the exception is recorded on the migration card and resolves when
  that domain work starts.
- Cross-service contracts written from here on use English field names; the
  eventos-facing ones change when eventos migrates, in one PR across producer and
  consumers as the platform `CLAUDE.md` requires.

**One exception: the Dominican cédula is `cedula`** (amended 2026-10-17, PGTI-227). Types,
properties, columns, JSON fields and error codes name it `cedula` (`Cedula`, `cedula_sealed`,
`cedulaMasked`, `collaborator.duplicate_cedula`), without the accent, so the client's reason
for English identifiers, special characters, does not apply. It is the proper name of one
national document, the way US code writes `ssn` rather than a translation: `nationalId` would
be vaguer, a passport is a national ID too, and it would sit beside `passport` and `visa` as
the only translated document name. **It is one word, not a new split**, so this ADR is
amended rather than superseded; a second Spanish identifier takes the supersede, as
ADR-0009 already says of its own carve-outs. Every other document name stays English,
the carnet included.

Added 2026-07-29, when the API documentation prose rule above was recorded:

- **`/scalar` is the authority on endpoint rules.** It is generated from the code
  that enforces them, which makes it the copy most likely to be corrected when a
  rule changes. `docs/ARCHITECTURE.md` and the Bruno collection are subordinate
  and may lag; where they disagree with the OpenAPI document, the OpenAPI
  document is right. This is a ruling about ownership, not a promise that the
  others were swept.
- **Documentation prose is impersonal — no second person.** The client is the
  Ministerio de Turismo de la República Dominicana (tuteo), while the existing
  Spanish corpus was written in Rioplatense voseo. Impersonal register is the
  standing convention for API reference documentation and is dialect-neutral, so
  the question does not return. Conversational documents (onboarding guides) are
  exempt; this binds the reference surface only.
- **Anything a rule depends on is machine-rendered, never prose.** The required
  role comes from the endpoint's authorization policy via an operation
  transformer, and status codes come from `.Produces` declarations. Hand-typing
  either into a Spanish string would recreate the drift this ADR's own migration
  produced — the permission roles were renamed and the Bruno prose did not
  follow.
- **Each glossary entry records its canonical Spanish rendering.** English
  identifiers alone were not enough: ADR-0008 sends the UI, the logs, the error
  text, and now the API documentation to Spanish without saying which Spanish
  word. Per-context `GLOSSARY.md` files are bilingual from here on, and their
  `_Avoid_` lists are scoped to identifiers. The verbs come from the
  pre-migration route segments, which were the client's own vocabulary.
