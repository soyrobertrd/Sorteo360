# The Raffle, its Rounds and its Prizes — Spec

The core model. An event has at most one raffle. The raffle holds rounds, rounds hold prizes, and
every prize has exactly one winner. It moves through three states, its prizes through three, and the
two axes never derive from each other.

**A prize is what one winner takes home, not one object.** "3 botellas de ron" is a single prize
handed whole to one person; five tote bags are five prizes with five winners. How many physical
things a prize contains is prose in its name, and nothing in this service reads it.

The reference vertical is `ServiceSupplier` and `Quote` in events-service. `QuoteStatus` is the shape
the state guards copy, and `QuoteWithCandidates` is the shape the aggregate copies — whole-aggregate
rules live on the aggregate root, rules needing other rows live in the service. `QuoteDecision` and
`IApprovalGate` are **not** copied: nobody approves a raffle.

Settled elsewhere and not re-argued here: ADR-0022 (where the raffle lives, the freeze, the snapshot
exception to ADR-0013, the two seams), ADR-0009 (PATCH), ADR-0010 (roles), ADR-0008 (English domain
names). Qualification is `01`, drawing a prize is `02`, the evidence trail is `03`.

**Amended 2026-10-01: "award" is now "draw".** The row recording one attempt to give a prize away was
`prize_award`. It is now `prize_draw` (`PrizeDraw`), because `POST …/draw` is what writes it, and
because "award" also named DO Travel Awards. Two codes were renamed with it: `PRIZE_ALREADY_AWARDED`
is `PRIZE_HAS_DRAWS`, and `RAFFLE_HAS_AWARDS` is `RAFFLE_HAS_DRAWS`. They do not merge into
`PRIZE_ALREADY_DRAWN`: that code is about a **live** draw, while these two are about **any** draw,
voided ones included. In `03`, the lookup route is now `/v1/prize-draws/by-code/{code}` and the
winner email type is `events.prize.won`. Nothing had been built under the old names.

**Amended 2026-10-04 (PGTI-120 grilling): what "undrawn" means in §4.** A prize may be edited or
deleted only while it has no draw at all, live or voided (`PRIZE_HAS_DRAWS`). A round may be renamed
only while none of its prizes has a draw, because its name is a heading on the acta; that is
`PRIZE_HAS_DRAWS` too, with a message about the round. A round may be deleted only while empty
(`ROUND_NOT_EMPTY`), as before. Prize update and delete, and round update, lock the raffle row and
check under the lock (ADR-0028), so an edit racing a draw either runs first or is refused.

## Decisions behind this shape

Settled with the user on 2026-09-20 in a grilling session on PGTI-34. Two of them depart from what
was written down before, and both are recorded as deviations on that ticket:

- **Three states, not four.** The `configured` state named in the ticket body permitted nothing
  `draft` did not. §2.
- **Three visual styles, not the four in Decision 16.** The fourth has no writer. §7.

Everything else refines the 28 Decisions of Record rather than changing them.

## 1. The four tables

| Table | Grain | Holds |
| ----- | ----- | ----- |
| `raffle` | one per event | `event_id`, `state`, the presentation settings, the two rule toggles, the six authority fields, `started_at/by`, `completed_at/by`, `acta_stored_file_id` |
| `raffle_round` | one per grouping | `raffle_id`, `number`, `name`, `description` |
| `prize` | one per thing won | `round_id`, `name`, `description`, `value` (free text, ≤ 100, printed as typed), `sponsor_company_id`, `sponsor_contact_id` (both nullable, ids only, §8), `sponsor_logo_file_id` (nullable, a `stored_file`, only with a company, §8), `created_at` |
| `prize_draw` | **one per draw attempt** | `prize_id`, the participant snapshot, `drawn_at/by`, `eligible_pool_size`, `redemption_code`, the void columns, the redemption columns |

Four tables, and no fifth. **There is no `prize_void` audit table.** A void is an outcome recorded on
the draw row it voided, so the history of a prize is the history of its draw rows and cannot drift
from a status column beside it — the same reasoning `convocatorias/03` §1 uses to refuse a
launch-event table.

Two constraints carry the grain rather than leaving it to the code:

- `raffle` is **unique on `event_id`**. That index is what "at most one raffle per event" means;
  nothing in the code says it.
- `raffle_round` is **unique on `(raffle_id, number)`, `DEFERRABLE INITIALLY DEFERRED`**, plus
  `ck_raffle_round_number` (`number >= 1`). That index is where "`1..n`, no gaps" lives. It is
  deferred so the renumber after a delete can shift every later round in one statement, and EF
  cannot declare it, so the migration adds it by hand.
- `prize_draw` has a **partial unique index on `prize_id WHERE voided_at IS NULL`**. Two live draws
  on one prize is a state the acta cannot represent, so the database refuses it.

The participant snapshot tables belong to qualification and are specified in `01`.

**The snapshot on `prize_draw` stores names, not only ids** — `winner_name`, `winner_company`,
`winner_email` beside `winner_participant_id` (built as `winner_attendee_id`, `02`'s 2026-10-04 amendment). This is ADR-0022's bounded exception to ADR-0013, and
it stops here: a legal document must stay legible years later, when the person may have been renamed,
soft-deleted or erased. Everything else in this service resolves display text at read time,
unchanged.

## 2. Raffle states

Three. `draft` is where the raffle is built, `in_progress` is the evening itself, and `completed` is
the signed-off record:

| From | To | On |
| ---- | -- | -- |
| `draft` | `in_progress` | `start` |
| `in_progress` | `completed` | `complete` |

`completed` is terminal **for drawing**, and says nothing about redemption (§3).

**Every transition is an explicit `POST {id}/verb`.** Nothing derives state from its data. The mock
does the opposite — a raffle becomes `configurado` the moment a first prize is added and
`en_progreso` after a first draw (`mockSorteo.ts:396, 495-500`) — and that cannot work here, because
the acta has to state how many people were in the drum, and that number needs a `started_at` and a
`started_by_id` beside it. A freeze that is a side effect of drawing prize 1 has no actor, no time of
its own, and walks backwards when prize 1 is voided.

**There is no `configured` state.** It permitted every operation `draft` permitted — everything
editable, the participant list still live — so it was one state wearing two badges, with no verb for
the transition into it. Readiness is computed on the read instead, as `isReadyToStart` plus the
reasons it is not, which answers the question a stored state only approximated: a stored flag says
somebody pressed a button, a computed one says the thing is runnable right now.

The qualification-side reasons are `01` §8.1's. The prize side adds exactly one, and it is a
**warning, not a blocking reason**: `no_prizes`. A raffle may start with no prizes, because §4 lets
prizes be added while `in_progress`, and a guard at `start` would contradict the surprise prize.
There is no `no_rounds`: a prize cannot exist without a round, so `no_prizes` already covers it.
Settled on PGTI-38.

**There is no `cancelled` state.** A raffle nobody ran stays in `draft` for ever, which is the
record. Adding one later is additive.

**`start` is the freeze.** Whoever is checked in at that instant is in the drum; a late arrival is
not. Starting *is* the refresh and there is no separate refresh action (ADR-0022, Decision 7). What
the freeze writes is `01` §4.

*(Amended 2026-10-04 by PGTI-118.)* **`start` answers `200` with the raffle**, the same body as
`GET …/raffle`: the new state, `startedAt`, `startedById` and the `warnings` in force at the freeze.
The operator's next screen is the raffle anyway, the answer cannot drift from the read, and the
warnings the acta will state are on the screen at the moment the drum closes.

*(Amended 2026-10-06 by PGTI-122.)* **`complete` answers `200` with the raffle**, the same body as
`GET …/raffle`: the new state, `completedAt`, `completedById` and the `notarialRecords` it just
wrote, the same reasoning as `start`.

**`complete` is allowed with prizes still undrawn.** Evenings end. A prize whose winner never showed
and which nobody chose to redraw is a real outcome, and the acta states it: N drawn, M not.
Refusing would leave the operator drawing a prize they had already decided not to give away, to satisfy a
guard, putting a false line in a notarised document. `complete` is also what generates and stores the
acta (`03`), which is why the authority fields are required here and nowhere else (§6).

## 3. The prize axis

A prize's state is **derived from its draws**, never stored:

| Derived | When |
| ------- | ---- |
| `undrawn` | no live draw |
| `drawn` | a live draw, not redeemed |
| `redeemed` | a live draw with `redeemed_at` set |

A draw is live until `voided_at` is set. Voiding is not a deletion: the row stays, carrying
`void_reason`, `voided_at` and `voided_by_id`, and the prize drops back to `undrawn` with a second
draw row following it when it is drawn again. That is what lets the acta print both attempts —
"Botella de ron — sorteada 20:14, María Pérez; declarada desierta 20:16 (ausente); re-sorteada 20:17,
Juan Gómez" — which Decision 15 requires and the mock cannot do, because `declararPremioDesierto`
wipes the winner fields back to empty (`mockSorteo.ts:555-588`).

**The two axes never derive from each other.** A `completed` raffle still accepts a handover, because
winners collect prizes at a desk days later; a `drawn` prize says nothing about whether the evening
is over. Conflating them would make the acta's generation close redemption, and a signature collected
next Tuesday would have nowhere to go.

## 4. What each state permits

| | `draft` | `in_progress` | `completed` |
| --- | --- | --- | --- |
| `PATCH` the raffle | yes | yes | no |
| add a round or prize | yes | **yes** | no |
| edit or delete a prize | yes | only if it has no draws | no |
| rename a round | yes | only if none of its prizes has a draw | no |
| delete a round | yes | only if empty | no |
| `start` | yes | no | no |
| `draw`, `void` | no | yes | no |
| `complete` | no | yes | no |
| `handover` | no | yes | **yes** |

**Prizes may be added while `in_progress`.** Surprise prizes are real: a sponsor walks up at 20:30
with something unannounced. Refusing it does not stop it happening — it pushes it off the system, so
the prize is written onto the acta by hand or goes unrecorded. Every prize carries `created_at`, so
the document shows which appeared mid-evening, with its draw a minute later. The mock supports this
too, by creating a prize on the fly when an unknown id is drawn (`mockSorteo.ts:474-492`); here it is
an ordinary `POST` before an ordinary draw, not a side effect of the draw.

**A surprise sponsor who is not in the CRM yet is not solved, by decision (PGTI-142, 2026-10-27).**
Their prize goes in unsponsored: the sponsor is optional, and nothing else about the prize waits on
it. Creating the company mid-event needs a CRM write role raffle operators may not hold, and once
the prize is drawn it can no longer be edited to add the sponsor. Revisit when MITUR asks for it.

**A prize with any draw against it, live or voided, cannot be edited or deleted.** Editing its name
or value rewrites what somebody won in front of witnesses, which is the one edit that makes
the signed document a lie. `PRIZE_HAS_DRAWS`, `ErrorKind.Conflict`.

**A round with prizes in it cannot be deleted.** `ROUND_NOT_EMPTY`. The mock instead reassigns
orphaned prizes to the first surviving round and renumbers (`mockSorteo.ts:340-364`); that silently
moves prizes into a bucket the operator did not choose, and on an acta the bucket is a heading
somebody reads aloud. Empty the round first.

**Rounds are renumbered on delete**, so `number` is always `1..n` with no gaps. It is display order
and the order the evening runs in, not an identity.

**`number` is the service's, never the request's.** A new round takes `max + 1`; no request carries a
number, and there is no reorder. Round creates and deletes lock the raffle row (`SELECT … FOR
UPDATE`) for their transaction, so two concurrent creates cannot take the same number and a
renumber cannot run under another write.

**A prize may move between rounds of its raffle.** `roundId` is part of the prize's complete
editable representation, so it is required on `PATCH` and changing it is the move. A round of another
raffle is `404 ROUND_NOT_FOUND`, like any id that is not this raffle's.

Creating a raffle creates no round. The server does not invent round names any more than prize
names (§8).

## 5. The event gates the front half and never the back half

Creating or editing a raffle, its rounds and its prizes, and `start`, are refused on a `closed` or
`cancelled` event with the existing `EVENT_NOT_EDITABLE` (`EventService.cs:193-195`). Building a
raffle onto an event that is over is a mistake every time.

**`draw`, `void`, `complete` and `handover` consult the event's status not at all.** Closing an event
is what materialises its cost, and it is entirely plausible that happens before the acta is finished,
or that a prize is collected at a desk a week later. If any of those four read the event's state,
closing an event would strand a raffle mid-evening with no way to produce the document a notary is
waiting to sign. **A raffle that legitimately started must always be able to finish**, including on a
cancelled event: prizes were drawn in front of witnesses, and the record of that has to be
completable whatever later happens to the event around it.

`start` deliberately does **not** require the event to be `in_progress`. Operators do not reliably
press "start event" before the evening begins, and a drum that will not open because of a status
nobody remembered to set is a failure in front of a room.

## 6. Authorities

One notary and two witnesses, each a `name` and an `identity_document` — six columns, not a
`raffle_witness` table. The shape is fixed at three people by the client's own process, and a table
would invite a fourth that the acta has no line for.

`identity_document` is **free text and opaque to this service**. What it will hold — cédula,
passport, colegiatura number — is not yet known, and a typed discriminator can be added later without
moving the data.

**All six are required at `complete` and at no other point.** `RAFFLE_AUTHORITIES_REQUIRED`,
`ErrorKind.Conflict`. The draw does not care who the notary is; the document does, and `complete` is
where the document is generated, so the guard and the need sit in the same place. Requiring them at
`start` would block an evening because a witness had not been typed in, in front of a room, for a
document that will not exist for another hour.

They must be required **somewhere**, though, and this is the spec's only outright correction of mock
behaviour rather than a gap in it: `ActaNotarialModal.tsx:330-351` bakes in placeholder names — "Dr.
Notario Público Actuante", "Comité Organizador MITUR" — when the fields are blank. A generated legal
document that invents the name of the notary attesting it is a defect, not a default.

## 7. Presentation settings

Three, stored and **never read by domain logic**: `visual_style`, `show_prize_on_screen`,
`animation_seconds`. Decision 16 says the backend saves the style and does not care, and the same
holds for the other two.

They are stored for one reason: during a live draw there are **two screens that must agree** — the
operator's laptop and the projector — and a page reload mid-evening must not silently change the
animation length or start revealing the prize early. Settings two devices have to agree on cannot
live in one device's memory. The mock's screen mode (`misma_pantalla` | `proyector`) is the opposite
case, per-device by definition, and correctly stays a launch argument that is never persisted
(`IniciarSorteoModal.tsx:25-28, 48`).

`visual_style` stores **three** values, guarded by `ck_raffle_visual_style`:

| Stored | The mock's |
| ------ | ---------- |
| `roulette` | `ruleta` |
| `ball_tombola` | `tombola_bolas` |
| `sequential_names` | `nombres_secuenciales` |

Decision 16 lists a fourth, `tombola`. It is legacy: creation and the launch modal are typed to
exclude it and `IniciarSorteoModal.tsx:42` rewrites it to `tombola_bolas` on read. Persisting a value
no writer emits means a CHECK constraint defending a state that can only arrive by hand.

`requires_presence_confirmation` is stored on the same terms — inert, for the two screens. It tells
the client whether to prompt the operator that the winner is in the room. It is **not** enforced,
because its enforcement already exists under another name: a no-show is a void with a reason (§3,
Decision 15), which produces exactly the acta line that is wanted. A `pending_confirmation` draw
state would duplicate that and wedge the raffle mid-evening when an operator moved on without
confirming.

The two anti-duplicate rules are toggles, per Decision 14: `one_prize_per_person` defaults **on**,
`one_prize_per_company` defaults **off**. What they do to the eligible pool is `02`.

## 8. Prize creation carries a quantity

`POST .../prizes` takes `{ roundId, name, description, value, quantity }` and creates
`quantity` **identical rows**. `quantity` is required and runs **1 to 50** inclusive, else `400`: a
typo cannot mint 500 rows, and more than fifty is a second `POST`.

The lengths, the same ones `03` uses: a round's `name` is required, ≤ 200, and its `description`
optional, ≤ 1,000; a prize's `name` is required, ≤ 200, `description` optional, ≤ 1,000, `value`
optional free text ≤ 100 (`'RD$8,000'`, stored and printed as typed, since nothing computes on it).

**A prize may carry a sponsor: a CRM company plus an optional contact** (PGTI-142), stored as
`sponsorCompanyId` and `sponsorContactId`, ids only, and resolved at read time (ADR-0013), never a
typed name. Both travel in the create and the `PATCH` body, so omitting them clears them (§9,
ADR-0009), they lock with the rest of the prize once it has a draw (§4), and a create with
`quantity: 5` gives all five rows the same sponsor. A contact without a company is `400`.

Writes check the CRM live and fail closed, as the service supplier does: `409
COMPANY_NOT_AVAILABLE` for a company id that never existed (a closed company still passes), `409
COMPANY_CONTACT_NOT_AVAILABLE` for a contact that does not exist or is not currently affiliated with
that company, and `503 CRM_UNAVAILABLE` when the CRM does not answer. Every route that returns the
raffle resolves the sponsor's company name from the cached company directory and the contact's
name live, carries `X-Crm-Directory-Status`, and answers `200` with a `null` name rather than
failing. The surprise sponsor not in the CRM goes in unsponsored (§4), and so has no logo either.

**The sponsor's logo belongs to the prize**, one per prize and never shared: `prize.sponsor_logo_file_id`
points at a `stored_file`. It is uploaded on its own route, `POST .../prizes/{prizeId}/logo`
(multipart, field `file`, `raffles:write`), because a file never travels in an owner's JSON body
(ADR-0014); a second upload replaces the first, which stays as an unlinked file, as an event's
cover does. The checks run in this order and all before a byte is stored: the event and the raffle
exist and accept setup changes, the prize exists, it has no draw (`409 PRIZE_HAS_DRAWS`), it has a
sponsor (`409 PRIZE_NOT_SPONSORED`), and the bytes are an image (`400 FILE_TYPE_NOT_ALLOWED` for a
PDF). A store that does not answer is `503 FILE_STORE_UNAVAILABLE` and writes nothing.
`DELETE .../prizes/{prizeId}/logo` unlinks it, `204` whether or not there was one, and never deletes
the file. A `PATCH` that clears the sponsor company or changes it to another unlinks the logo in the
same statement; one that keeps the company keeps it. Every read that returns a prize carries
`sponsor.logo: { fileId, fileName, missing }` or `null`, and the bytes are served by
`GET /v1/files/{fileId}/content`. The acta and the winner email do not print it.

The quantity spinner is the safeguard that makes §0's distinction legible at the only moment it can
be made. "3 botellas de ron" is one prize with `quantity: 1`; five tote bags are `quantity: 5`. An
operator who types one prize called "5 tote bags" gets one winner, and the backend cannot know that
was a mistake — so the contract asks "how many winners should this produce" every single time a prize
is created, rather than trusting a client to expand the list as the mock does today
(`ConfigurarPremiosModal.tsx:52-69`).

**The server does not invent names.** No `"Tote bag #1"…"#5"` — that is the mock decorating for
display. Five rows all named "Tote bag", each with its own id, winner and redemption code. The acta
reads as five lines saying "Tote bag — María Pérez", which is correct and needs no ordinals.

**`quantity` is create-only and never appears on any update path.** One row cannot retroactively
become five; that is four new prizes, not an edit. Adding a sixth tote bag later is a second `POST`
with `quantity: 1`. This sits inside ADR-0009 rule 3, and keeping it off the update path means a
prize's complete editable representation never has to explain what editing a quantity would mean.

This one create endpoint returns the **created collection**, not the bare `Guid` its siblings return
(`QuoteEndpoints.cs:239-240`). One call creating five rows has five ids, and inventing a primary one
would be a lie about which.

## 9. PATCH carries the whole raffle

One `PATCH .../raffle`, carrying the complete editable scalar representation: the three presentation
settings, `requiresPresenceConfirmation`, the two rule toggles, the six authority fields. Per ADR-0009
an omitted optional field is **cleared, not preserved**, and every value-type property on the request
is nullable with requiredness decided by the validator.

**Rounds and prizes are not in that body.** They are sub-resources with their own endpoints, exactly
as candidates and proposals hang off a quote. If they appeared here, omitting them would delete them.

The sharp edge is real and belongs in the handoff (§12): the mock edits these scalars from three
independent panels, each calling `updateSorteo` with a partial object
(`SorteoDetailPage.tsx:755, 772, 789, 931-936`). Under ADR-0009 each of those calls blanks the other
two groups. Three narrow per-concern endpoints were considered and rejected: ADR-0009 names its own
remedy for concurrent whole-resource edits, and it is `xmin`, not partial updates — the entities
already carry it as a row version (`QuoteConfiguration.cs:59-60`).

## 10. Endpoints

All under `v{version:apiVersion}/events/{eventId:guid}/raffle`. Reads carry no policy — the fallback
policy requiring an authenticated user covers them, per `AuthorizationPolicies.cs:46-49` and the
reasoning at `QuoteQueryEndpoints.cs:41-43`.

| Route | Role | Notes |
| ----- | ---- | ----- |
| `GET ""` | — | the raffle with its rounds, prizes and derived prize states; `404` if none |
| `POST ""` | `raffles:write` | creates the `draft`; `409 RAFFLE_ALREADY_EXISTS` on a second |
| `PATCH ""` | `raffles:write` | §9. Not `completed` |
| `POST "start"` | `raffles:draw` | `draft` → `in_progress`. Freezes the list (`01` §4). `200` with the raffle (§2) |
| `POST "complete"` | `raffles:draw` | `in_progress` → `completed`. Generates the acta (`03`) |
| `POST "rounds"` | `raffles:write` | |
| `PATCH "rounds/{roundId:guid}"` | `raffles:write` | |
| `DELETE "rounds/{roundId:guid}"` | `raffles:write` | `409 ROUND_NOT_EMPTY` if it holds prizes |
| `POST "prizes"` | `raffles:write` | §8. Takes `quantity`, returns the created collection |
| `PATCH "prizes/{prizeId:guid}"` | `raffles:write` | `409 PRIZE_HAS_DRAWS` if it has any draw |
| `DELETE "prizes/{prizeId:guid}"` | `raffles:write` | same refusal |
| `POST "prizes/{prizeId:guid}/logo"` | `raffles:write` | §8. Multipart; replaces the sponsor's logo. `409 PRIZE_NOT_SPONSORED`, `409 PRIZE_HAS_DRAWS` |
| `DELETE "prizes/{prizeId:guid}/logo"` | `raffles:write` | §8. Unlinks it; the file survives |
| `POST "prizes/{prizeId:guid}/draw"` | `raffles:draw` | **no request body.** `02` |
| `POST "prizes/{prizeId:guid}/void"` | `raffles:draw` | body `{ reason }`. `02` |
| `POST "prizes/{prizeId:guid}/handover"` | `raffles:draw` | `03`. Permitted while `completed` |

**`start` and `complete` are `raffles:draw`, not `raffles:write`.** They are acts of running the
evening, not of configuring it. `start` is the moment the drum closes in front of a room, and someone
trusted only to type prize names should not be able to slam it shut while the operator is still
moving people across with manual overrides. `complete` produces the signed document, and a role
already trusted to hand a prize over with a signature — Decision 21 explicitly refused splitting that
out — is trusted to close the acta.

**Both roles sit on `opt_coordinator`, `opt_director` and `central_admin`**, so nobody is narrowed on day one.
The split exists so a draw-only operator is expressible later as a console change with no code behind
it, which is the trade ADR-0010 exists to make.

**`POST .../draw` takes no request body**, ever. The instant it accepts a `participantId` "for
convenience", every other guard in this spec is decoration: the server's pick, the frozen snapshot
and the recorded overrides all become suggestions. The winner is computed server-side from the frozen
list with `RandomNumberGenerator`, not `System.Random` — "the draw used a seeded PRNG" is an
unpleasant sentence to have to explain to a notary — and the draw records `eligible_pool_size`,
which is the number an auditor checks against the snapshot. No seed is recorded: with a CSPRNG there
is not one, and a recorded seed from a seeded PRNG would let anyone who saw it beforehand predict the
result, weakening the thing it appears to strengthen. The real control is the notary and two
witnesses in the room (Decision 21).

## 11. The realm diff

Two new function roles in `infra/keycloak/pgti360-realm.json`, added to each of the
`opt_coordinator`, `opt_director` and `central_admin` composites, which are flat lists that do not
inherit from one another:

| Role | Guards |
| ---- | ------ |
| `raffles:write` | create and edit the raffle, its rounds and its prizes; editing the qualified list (`01`) |
| `raffles:draw` | `start`, `draw`, `void`, `handover`, `complete` |

There is no `raffles:read`. Composite names are not touched — they are the provisional layer
(ADR-0010), and nothing here needs them to move.

**This lands twice**, per the root `CLAUDE.md`: in the realm JSON, for environments created later,
and as a console step in `docs/deploy-with-dokploy.md`, for the three already running. The JSON is
imported only when the realm does not exist and is never re-applied (ADR-0016), so a role that lands
only in the file reaches no deployed environment.

## 12. English ↔ Spanish, for the frontend

Domain names are English platform-wide (ADR-0008). The Spanish below is presentation and stops at the
UI. This table is the handoff to the frontend owner; per Decision 26 a backend developer may do the
wiring but **may not change any aspect of the UI**.

| Backend | Mock's Spanish |
| ------- | -------------- |
| `Raffle` | `Sorteo` |
| `RaffleRound` | `Ronda` |
| `Prize` | `Premio` |
| `PrizeDraw` | — (was denormalised onto `Premio`) |
| `winner*` | `ganador*` |
| `redemptionCode` | `codigoCanje` |
| `handover` / `redeemed` | `entrega` / `entregado` |
| `voided` | `desierto` |
| `notaryName` | `notarioNombre` |
| `witnessOneName` / `witnessTwoName` | `testigo1` / `testigo2` |
| `visualStyle` | `visualizacion` |
| `onePrizePerPerson` / `onePrizePerCompany` | `reglaUnPremioPorPersona` / `reglaUnPremioPorEmpresa` |
| `draft` / `in_progress` / `completed` | `borrador` / `en_progreso` / `completado` |

**Six changes the frontend has to absorb**, all of them his card:

1. **`configurado` is gone.** `EstadoSorteoBadge` drops to three states; `isReadyToStart` replaces it.
2. **`PATCH` must send the complete scalar representation.** Three panels each sending their own
   slice will blank the other two. This is the single most likely way this module ships a data-loss
   bug.
3. **Prize creation posts `quantity`**; the server expands and returns the collection. No client-side
   expansion, no `#i` suffixes.
4. **`visualizacion` values become English codes**, and `tombola` is gone.
5. **The raffle has no `nombre` and no `fechaSorteo`.** Display comes from the event.
6. **The draw endpoint takes no body.** The winner arrives in the response; the roulette animates
   towards it.

Dropped entirely and not replaced: `participantesExternos` and the three participant modes
(Decision 5), `filtroTipos` (Decision 10), `eventoNombre` and `fechaEvento` (ADR-0013, resolved at
read time). Those belong to PGTI-41.

## Definition of done

- Every transition in §2 is covered by a test, and every transition **not** in the table is refused
  with a machine code. `start` twice is `409 RAFFLE_NOT_DRAFT`; `complete` on a `draft` is
  `409 RAFFLE_NOT_IN_PROGRESS`; any write to a `completed` raffle but a handover is
  `409 RAFFLE_COMPLETED`.
- A second `POST` to an event that already has a raffle is `409 RAFFLE_ALREADY_EXISTS`, and the
  unique index on `raffle.event_id` exists in the migration set — assert the constraint, not only the
  error.
- `prize_draw` has a partial unique index on `prize_id WHERE voided_at IS NULL`. A test inserts a
  second live draw directly and expects the database to refuse it.
- `POST .../prizes` with `quantity: 5` creates five rows with identical names, five distinct ids, and
  returns all five. `quantity` appears on no update request type anywhere in the solution — assert it
  as an absence.
- `POST .../prizes/{id}/draw` has **no request body type**. Assert that the endpoint's handler takes
  no `[FromBody]` parameter; a model binding a participant id must not compile.
- The draw uses `RandomNumberGenerator`; `System.Random` appears nowhere in the raffle namespace.
  `eligible_pool_size` is written on every draw.
- Editing or deleting a prize with a live draw, **and one with only a voided draw**, are both
  `409 PRIZE_HAS_DRAWS`. The voided case is the one that regresses.
- Deleting a round holding prizes is `409 ROUND_NOT_EMPTY` and moves nothing. Deleting an empty round
  renumbers the survivors to `1..n` with no gaps.
- Adding a round or a prize while `in_progress` succeeds; editing an undrawn one succeeds.
- `complete` with all six authority fields absent is `409 RAFFLE_AUTHORITIES_REQUIRED` and changes no
  state. `complete` with prizes still undrawn **succeeds**.
- A `completed` raffle accepts `POST .../handover` and refuses `POST .../draw`.
- Creating, editing and `start` on a raffle whose event is `closed` or `cancelled` are
  `409 EVENT_NOT_EDITABLE`. `draw`, `void`, `complete` and `handover` on the same event all succeed —
  this is the assertion that protects a raffle from being stranded by an event closing under it.
- A `PATCH` omitting an optional scalar clears it, and omitting a domain-required one is a `400`.
  Value-type properties on the update request are all nullable — assert it by reflection over the
  request type, as ADR-0009 rule 2 is invisible in any single test.
- A caller holding `raffles:write` but not `raffles:draw` gets `403` on `start`, `draw`, `void`,
  `handover` and `complete`, and `200` on every setup edit. The reverse holds.
- `infra/keycloak/pgti360-realm.json` contains both roles inside all three composites, and
  `docs/deploy-with-dokploy.md` carries the matching console step. Both in the same PR.
- No `raffle.name`, no `raffle.scheduled_at`, no `prize.quantity`, no `prize_void` table and no
  `tombola` value exist anywhere in the migration set. Assert the absences.
