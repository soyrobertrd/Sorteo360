# Raffles — Build Order

The work queue for the specs beside this file. Thirteen backend steps and one frontend card, in
dependency order, each sized to be finished, tested and reviewed on its own: **350–900 lines
including tests**, deliberately tighter than the tickets that shipped before it.

Read the specs for the *why* and the shapes; this page is the order to build in and what blocks what.
Settled with the user on 2026-09-26 in a grilling session on PGTI-38.

## Before you start

**Read these, in this order:**

1. `docs/dotnet-service-conventions.md` — the conventions every .NET service here follows, and the
   patterns deliberately **not** used: no MediatR or CQRS, no AutoMapper, no controllers, no global
   exception handler, no `IUnitOfWork` layer.
2. `events-service/CLAUDE.md` — this service's layout, and §"A quote's alerts…", whose rules step 13
   applies unchanged.
3. `events-service/GLOSSARY.md` — the domain vocabulary. `attendee`, `participant` and `the drum`
   are three different words (`01` §1).
4. `docs/adr/0022` — where the raffle lives, the freeze, the snapshot exception to ADR-0013, the two
   seams. Its amendment block is `01`'s.
5. The four specs, `00` to `03`. Later ones refine earlier ones and say so in their "Decisions behind
   this shape" section. **Where two disagree, the later spec wins** — `03` §1 replacing `00` §1's
   four tables with six is the example that matters.

**Your reference vertical is `Quote` and `ServiceSupplier` in events-service.** `QuoteStatus` is the
shape the raffle state guards copy, `QuoteWithCandidates` is the shape the aggregate copies,
`QuoteRepository.cs:371-374` is the stamp-the-parent concurrency idiom (the raffle's draw phase locks
instead, ADR-0028), `Core/Files/` is the upload
seam, and `QuoteNotifier` is the post-commit email. `QuoteDecision` and `IApprovalGate` are **not**
copied: nobody approves a raffle. When a spec and that code disagree on *structure*, the code wins; on
*behaviour*, the spec wins.

**Conventions worth knowing before step 1:**

- **Each step creates the schema it uses**, in its own migration, in the same PR as the behaviour
  and the tests. There is no up-front "all the entities" step: it would ship tables nothing reads,
  and freeze a design the later specs have already revised once.
- Errors carry a machine `code` alongside the HTTP status, emitted by the chassis as a ProblemDetails
  extension. An error that carries more than a code overrides `BaseError.ProblemExtensions`.
- Declaring `.WithValidation<TRequest>()` removes the inferred `200`, so success codes must be
  declared explicitly.
- Every outbound call goes through the chassis `AddServiceClient`, never a bare `HttpClient`.
- Every environment variable lands in `docker-compose.yml` **and** `docker-compose.dokploy.yml`.
- A realm change lands twice: in `infra/keycloak/pgti360-realm.json` and as a console step in
  `docs/deploy-with-dokploy.md` (ADR-0016).

**What already exists and should be reused, not rebuilt:** `StaleTolerantCache`
(`Infrastructure/Directories/`), the CRM service client from the OPT directory edge (ADR-0013),
`KeycloakAdminClient.GetUserAsync`, the file seam with `UploadPolicy` and `StoredFileReferences`
(ADR-0014), `INotificationClient`. crm-service already serves `POST /v1/contacts/mailability`.
MiniExcel and Sep are pinned at the root and used by convocatorias; events-service does not reference
them yet.

## The queue

```
1. Raffle ─┬─► 2. Rounds + prizes ─────────────────────────────┐
           │                                                    │
           ├─► 3. Attendance seam ─┐                            │
           │                       └─► 5. Qualification ─► 6. Overrides + CRM ─► 7. Start ─► 8. Draw ─┬─► 9. Void + reopen ─► 11. Complete + record ─┬─► 12. Reissue
           └─► 4. Awards (deferred)                                                                    │                                           └─► 13. Winner email ─► 14. Frontend
                                                                                                        └─► 10. Handover
```

The numbers are a sensible order for one developer. The arrows are the real blocking edges, and they
are what the Jira tickets carry: after step 1, steps 2, 3 and 4 are independent of each other.

### 1. The raffle aggregate — `00` §1, §2, §5, §6, §7, §9, §10, §11

The `raffle` table, unique on `event_id`, with its state column, the presentation settings,
`requires_presence_confirmation`, the two rule toggles and the six authority fields. `POST`, `GET`
and the whole-body `PATCH` under `/v1/events/{eventId}/raffle`. The event gate on the front half
(`EVENT_NOT_EDITABLE`). `ck_raffle_visual_style` with three values.

**The two roles, `raffles:write` and `raffles:draw`**, in `AuthorizationPolicies.cs`, inside the
`opt_coordinator` composite in the realm JSON, and as the console step in
`docs/deploy-with-dokploy.md`, all in this PR. Every later step only uses them.

`GET` returns the raffle with empty rounds; later steps grow its shape.

**Blocked by:** nothing.

### 2. Rounds and prizes — `00` §1, §4, §8

`raffle_round` and `prize`, their create/edit/delete endpoints, `quantity` on create only with the
created collection returned, renumbering on round delete, `ROUND_NOT_EMPTY`. `created_at` on every
prize, since the record marks mid-evening additions (`03` §4.2 item 9).

`PRIZE_HAS_DRAWS` is **not** here. Nothing can be drawn yet, so it lands in step 9.

**Blocked by:** 1.

### 3. The attendance seam — `01` §7

**Repurposed on PGTI-114 (2026-09-29): there is no stub.** Check-in (PGTI-127 → PGTI-130) ships
before any real event hosts a raffle, so an attendance upload would only ever have carried rehearsal
data, and at a real event it would have let walk-ins into the drum unchecked (`01` §7.1).

`IAttendanceSource` and its `Attendee` record in Core, mirroring `convocatorias/09` §7: unfiltered,
and no ceiling in the seam. `UnavailableAttendanceSource` in Infrastructure, registered in every
environment, failing every call with `503 ATTENDANCE_UNAVAILABLE` until PGTI-130 replaces it. No
route, no table, no upload, no new package.

**Blocked by:** 1.

### 4. The awards list — `01` §5

**Deferred on PGTI-115 (2026-10-01): nothing blocks on it any more.** `01` §5.2 already makes "no
list in force" a working state, with the awards step skipped loudly, so step 5 ships without it and
takes the seam itself. The cost is that every raffle runs without the DO Travel Awards filter until
this step lands. What follows is what remains here when it is picked up.

`UploadedAwardsMembership`, `awards_upload_batch` and `awards_member`.
`POST /v1/awards-members` replacing the whole list in one transaction, and `GET /v1/awards-members`
returning the batch metadata and a count, **never the addresses**.

Global, not per raffle: the routes sit outside `/events`.

**This is the step that brings MiniExcel and Sep into events-service.** It writes the spreadsheet
reader that step 3 no longer has; keep it free of anything awards-specific.

**Blocked by:** 1 (the `raffles:write` role).

### 5. Qualification and the draft read — `01` §2, §4, §5.2, §6, §8, §8.1; `00` §2

The pipeline as one pure Core function over attendance, awards membership and, from step 6,
overrides and mailability: `walk_in`, `contact_unresolved`, `not_awards_member`, `dominican_domain`,
first match wins. The awards step skipped, not emptied, when no batch is in force. The 5,000-attendee
ceiling and `ATTENDANCE_TOO_MANY_ROWS` are the raffle's, so they live here, not in the seam.

**The gates are enforced from the start, against a fake `IAttendanceSource`.** The registered source
refuses every call until PGTI-130, so every test of this step substitutes the seam; nothing is built
dormant and switched on later.

`GET /v1/events/{id}/raffle/attendees` in `draft`, computed on every read and **never written**,
behind `StaleTolerantCache` with `attendanceStatus` and `attendanceFetchedAt` in the body (`01` §8,
amended by PGTI-116: not `X-Crm-Directory-Status`, which is CRM's).
`isReadyToStart`, `blockingReasons` and `warnings` on `GET …/raffle`, **including the prize-side
`no_prizes` warning** `00` §2 gained on PGTI-38.

`do_not_invite` and `manually_excluded` are **not** here; the function leaves their slots in the order.

**`IAwardsMembership` lands here, not in step 4** (deferred 2026-10-01). The registered
implementation always answers "no list in force", so the awards step is skipped with its warning
in every environment until step 4 replaces it; the "a list matching nobody" case is tested against
a fake, like attendance.

Split point if it will not fit: the pure pipeline and its unit tests first, then the read.

**Blocked by:** 3.

### 6. Overrides and the CRM gate — `01` §3, §4.2, §9

`raffle_attendee_override` and its two endpoints, email in the body. The manual step in the pipeline,
with `overruled_reason`. **The CRM mailability client** on the existing CRM service client —
`POST /v1/contacts/mailability` in batches of at most 1,000 ids, one after another, failing whole
if any batch fails (`01` §3.1) — and the `do_not_invite` step **after** the manual one. `BrokenCircuitException` mapped to `CRM_UNAVAILABLE`.

The four override refusals: `PARTICIPANT_WALK_IN`, `PARTICIPANT_DO_NOT_INVITE`,
`PARTICIPANT_CONTACT_UNRESOLVED`, `PARTICIPANT_NOT_CHECKED_IN`.

The CRM branch is exercised against a fake attendance source and a WireMock CRM, so C3 changes where
the attendance comes from, not whether the code works.

**Blocked by:** 5.

### 7. `start`, the freeze — `01` §4, §4.1, §8; `00` §2, §4

`POST …/raffle/start`: the pipeline once more with **live** reads, bypassing the cache, and
`raffle_attendee` written for every checked-in person, qualified or not. `awards_batch_id` on the
raffle, `started_at/by`. `409 RAFFLE_NO_PARTICIPANTS`, `503 ATTENDANCE_UNAVAILABLE`,
`503 CRM_UNAVAILABLE`, and a refused `start` writes **no rows**. Serialised on the raffle row.

The attendees read switches to `SELECT raffle_attendee` once `in_progress`. Overrides refused after
`start`. Rounds and prizes may still be added.

**Blocked by:** 2, 6.

### 8. The draw — `02` §1, §2, §4, §5, §7, §7.1; `03` §2.2, §2.3; `00` §10

`prize_draw` with its partial unique index on `prize_id WHERE voided_at IS NULL`, the winner
snapshot, `eligible_pool_size`, and the four void columns the index and the pool read (no verb
writes them yet). **The pool function** — one function, called by the draw and by
`GET …/prizes/{id}/eligible` — with company matching (`02` §1.1). `POST …/draw` with no body,
`RandomNumberGenerator` over the pool ordered by attendee id, locking the raffle row (ADR-0028).
`RAFFLE_POOL_EXHAUSTED` with its counts, `PRIZE_ALREADY_DRAWN` carrying the live draw.

**The redemption code is generated here**, inside the draw transaction, with its global unique index
— it is a non-null column on the row this step creates. The lookup that reads it is step 10.

`GET …/raffle` now carries each prize's derived state and draw history.

Split point if it will not fit: the pool function and its unit tests first, then the endpoints.

**Blocked by:** 7.

### 9. Void, reopen and the guards that need draws — `02` §1.2, §3, §6, §7; `00` §4

`POST …/void` with the closed code and the note, writing the four void columns, the `ineligible` reach across
the raffle. `POST …/reopen`, permitted only with zero draw rows, deleting `raffle_attendee` and
keeping overrides. `RAFFLE_RULES_LOCKED` on a `PATCH` that changes a toggle while `in_progress`.
`PRIZE_HAS_DRAWS` on editing or deleting a prize with any draw, live or voided, and on renaming a
round holding one; all three lock the raffle row (ADR-0028, amended by PGTI-120).

**Blocked by:** 8.

### 10. Handover and the code lookup — `03` §2.4, §3

`prize_handover`, its partial unique index, `POST …/handover` as one multipart call with a PNG-only,
1 MB signature through `UploadPolicy`, and `POST …/handover/reverse`. Both lock the raffle row. The
prize axis re-derived against the new table. `GET /v1/prize-draws/by-code/{code}`, flat, normalised,
answering `voided` rather than hiding it, **never redeeming**.

`prize_handover` goes into `StoredFileReferences` here.

**Blocked by:** 8.

### 11. `complete` and the notarial record — `03` §1, §4.1–§4.4, §4.6; `00` §2, §6

`POST …/raffle/complete`: `RAFFLE_AUTHORITIES_REQUIRED` and `RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED`
(`pickup_instructions` joins the raffle and its `PATCH` body here), the PDF rendered behind a Core
seam, saved through `IFileStore`, `notarial_record` version 1, and `state = completed` in one
transaction. Fails closed on the store. Operator names via `KeycloakAdminClient`, never failing.
`notarialRecords` on `GET …/raffle`.

**Two packages are pinned at the root in this step:** PDFsharp-MigraDoc (ADR-0024) in
Infrastructure, and **UglyToad.PdfPig in the test projects only**, for the golden test on the
extracted text that `03` requires and ADR-0024 relies on. Asserting on the MigraDoc document model
instead was rejected: it never proves the text reached the page.

The biggest step on the list. Split point if it will not fit: the transition and its guards with a
minimal record first, then the full §4.2 layout and its golden test.

**Blocked by:** 9 (the record prints voids and their notes). Step 10 need not have landed: the
record says nothing about handovers.

### 12. Reissue — `03` §4.5

`POST …/raffle/reissue`, `completed` only, the six authority fields in one body, version *n + 1*
through the same rendering function, the "reemplaza la versión" line, no email.

**Blocked by:** 11.

### 13. The winner email — `03` §5

The `events.prize.won` templates, **`es` and `en`**, seeded by `InsertData` in their own migration
with a frozen seed class and a golden-file test. Sent after `complete`'s commit, one per live draw,
idempotency key `events:raffle:{raffleId}:draw:{drawId}:won`, swallowed per ADR-0020. Language from
`Event.Language`, falling back to `es`. `winner_notification_id` and `winner_notification_status` on
the draw.

**The copy is provisional.** The agent writes both languages, with English back-translations in the
PR, and Garry reviews it there. Client changes later are one seed migration and no code.

**Blocked by:** 11.

### 14. Frontend catch-up — Robert's, own card, may lag

Per the root `CLAUDE.md` exception. The four handoff lists, verbatim as the checklist: `00` §12, `01`
§10, `02` §9, `03` §9. Sits beside PGTI-41, which already owns removing the invented participant
sources. The most likely data-loss bug in the module is `00` §12 item 2: three panels each `PATCH`ing
their own slice.

**Blocked by:** 13, since `03` §9 describes the finished contract.

## Where each Definition-of-done bullet lands

Every bullet in the four specs' Definition of done belongs to exactly one step and is that ticket's
acceptance criterion. A bullet spanning several steps lands in the **last** step it needs; earlier
steps test their own part of it as they go, and the last one owns the whole assertion.

| Spec | Bullet | Step |
| ---- | ------ | ---- |
| `00` | every transition covered, every other refused (`RAFFLE_NOT_DRAFT`, `RAFFLE_NOT_IN_PROGRESS`, `RAFFLE_COMPLETED`) | 11 |
| `00` | second `POST` is `RAFFLE_ALREADY_EXISTS`, unique index on `event_id` asserted | 1 |
| `00` | partial unique index on `prize_draw`, second live draw refused by the database | 8 |
| `00` | `quantity: 5` creates five rows; `quantity` absent from every update type | 2 |
| `00` | draw endpoint has no body type | 8 |
| `00` | `RandomNumberGenerator`, no `System.Random`; `eligible_pool_size` always written | 8 |
| `00` | editing or deleting a prize with a live **or voided** draw is `PRIZE_HAS_DRAWS` | 9 |
| `00` | `ROUND_NOT_EMPTY` moves nothing; deleting an empty round renumbers `1..n` | 2 |
| `00` | adding a round or prize while `in_progress` succeeds; editing an undrawn one succeeds | 7 |
| `00` | `complete` without authorities is refused; with prizes undrawn it succeeds | 11 |
| `00` | a `completed` raffle accepts handover and refuses draw | 11 |
| `00` | event `closed`/`cancelled` refuses create, edit, `start`; allows draw, void, complete, handover | 11 |
| `00` | `PATCH` clears an omitted optional, `400`s an omitted required; value types nullable by reflection | 1 |
| `00` | `raffles:write` without `raffles:draw` is `403` on the five run verbs, and the reverse | 11 |
| `00` | both roles in the realm JSON and the console step, same PR | 1 |
| `00` | no `raffle.name`, `raffle.scheduled_at`, `prize.quantity`, `prize_void`, `tombola` | 2 |
| `01` | pipeline order fixed by a blacklisted person with a manual include being out | 6 |
| `01` | manual include of `.do` and of a non-member puts them in, with `overruled_reason` | 6 |
| `01` | `PARTICIPANT_WALK_IN` / `PARTICIPANT_DO_NOT_INVITE` write nothing | 6 |
| `01` | an override predating the blacklist survives, loses, and the frozen row says so | 7 |
| `01` | an `opted_out` contact is in the drum | 6 |
| `01` | `canBeEmailed: false` with `opted_out` qualifies | 6 |
| `01` | an id absent from the mailability response is not cleared | 6 |
| `01` | mailability ids go in batches of at most 1,000, in series (2,500 ids → 1,000, 1,000, 500); one failed batch fails the whole check | 6 |
| `01` | `checked_in = qualified + not_qualified` on every frozen snapshot | 7 |
| `01` | no batch in force: skipped, `start` succeeds, null batch id, warning; batch matching nobody: `RAFFLE_NO_PARTICIPANTS` | 7 |
| `01` | a second awards upload replaces the first; the old addresses are gone | 4 |
| `01` | an all-invalid file succeeds with `accepted: 0` | 4 |
| `01` | draft read with the seam down is `stale`; `start` with it down is `503 ATTENDANCE_UNAVAILABLE` and writes no rows | 7 |
| `01` | `start` never reads through the cache | 7 |
| `01` | overrides after `start` are `RAFFLE_NOT_DRAFT` | 7 |
| `01` | no `raffle_participant` table | 7 |
| `02` | the pool function unit-tested line by line, including company matching | 8 |
| `02` | `RAFFLE_POOL_EXHAUSTED` writes no row; its counts sum to `participants` | 8 |
| `02` | `PATCH` changing a toggle while `in_progress` is `RAFFLE_RULES_LOCKED` and applies nothing | 9 |
| `02` | the cross-prize race: one draw, one `RAFFLE_POOL_EXHAUSTED` | 8 |
| `02` | draw racing void: neither answers `CONCURRENCY_CONFLICT` | 9 |
| `02` | draw on a `drawn` prize carries the live draw's winner | 8 |
| `02` | void on `undrawn` is `PRIZE_NOT_DRAWN`; a void keeps the row | 9 |
| `02` | void on `redeemed` is `PRIZE_ALREADY_REDEEMED` | 10 |
| `02` | void `other` without a note, and an unknown code, are `400` | 9 |
| `02` | eligible `count` equals the next draw's `eligible_pool_size` | 8 |
| `02` | eligible response has no email field | 8 |
| `02` | `reopen` with zero draws; with one voided draw it is `RAFFLE_HAS_DRAWS` | 9 |
| `02` | `reopen` on a `closed` event is `EVENT_NOT_EDITABLE` | 9 |
| `02` | no `redraw`, no reset, no `Idempotency-Key` | 9 |
| `03` | the code matches `^RD-…$`; no `System.Random`, no clock | 8 |
| `03` | the code's unique index has no `raffle_id` | 8 |
| `03` | a void keeps its code; a redraw issues a different one | 9 |
| `03` | the lookup normalises, answers `drawn`/`redeemed`/`voided`/`404`, writes nothing, has no name parameter | 10 |
| `03` | a PNG under 1 MB hands over; JPEG, PDF, oversize and renamed files leave no row and no bytes | 10 |
| `03` | a handover missing the document or `receiverIsWinner` is `400` | 10 |
| `03` | a handover on a `completed` raffle and on a `closed` event succeeds | 11 |
| `03` | `reverse` keeps the row and returns to `drawn`; a second live handover is refused by the database | 10 |
| `03` | void racing handover: one success, one `PRIZE_NOT_DRAWN` or `PRIZE_ALREADY_REDEEMED` | 10 |
| `03` | voiding a `redeemed` prize is still `PRIZE_ALREADY_REDEEMED` | 10 |
| `03` | `complete` without `pickupInstructions` is refused; with a faulted store it is `503`, stays `in_progress`, and sends no email | 13 |
| `03` | a successful `complete` writes version 1, sniffing as PDF | 11 |
| `03` | the golden test on the extracted text, including its absences | 11 |
| `03` | an unresolvable operator id prints *usuario no disponible* | 11 |
| `03` | `reissue` refused while `in_progress`; writes version 2 naming version 1; `PATCH` on `completed` still refused | 12 |
| `03` | one email per live draw by `SendKeys`, with the key; none for voided, none on `reissue` | 13 |
| `03` | notification-service down: `complete` succeeds, `failed` recorded | 13 |
| `03` | `en` event gets `en`, `fr` gets `es` | 13 |
| `03` | `StoredFileReferences` lists `prize_handover` and `notarial_record` | 11 |
| `03` | no `acta_stored_file_id`, no redemption columns on `prize_draw`, no redeeming lookup, no raffle download route | 11 |

Plus, on every step: `dotnet test` green across the solution, `scripts/check.sh` passing (compose
parity included), a new migration for every schema change, and `/_alive` answering `200` after
`docker compose build events-service && docker compose up -d events-service`.

## What is not in this queue

- **Check-in and the scanner, C1 to C5** (`plans/raffles-and-frontend-wiring.md`, Phase 6). Spec'd
  by PGTI-39 (`docs/specs/convocatorias/09-check-in.md`) and PGTI-40, ticketed from there.
- **C3 (PGTI-130) replaces step 3's placeholder** with `ConvocatoriasAttendanceSource`, and deletes
  `UnavailableAttendanceSource` and nothing else. It is the first call on the Events → Convocatorias
  edge in `GLOSSARY-MAP.md`. It switches nothing on: the walk-in, `contact_unresolved` and
  `do_not_invite` gates are enforced from steps 5 and 6, so nothing earlier in this queue changes.
- **C4 as drafted contradicts `03` §2.4.** "Prize redemption scan endpoint" reads as a scan that
  redeems; `03` settled that a code lookup never redeems and a handover needs a receiver, a document
  and a signature. **Settled by PGTI-40** (`convocatorias/10-scanner-terminal.md` §8): the scanner
  needs nothing on the prize side beyond PGTI-121's lookup and the handover, so C4 is the door Event
  list instead, PGTI-131.
- **Read authorisation for `event_staff`** (`03` §7). A blocker on C2, not on anything here: until
  that composite exists, "authenticated" means MITUR staff.
- **Uploading the ink-signed record** (`03` §4.1), the planned addition, deliberately not built.
