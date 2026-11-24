# Drawing a Prize — Spec

Who can win a given prize at a given moment, how the server picks one, and how a draw is undone.

The draw is one function, used twice: once to show the operator who is in the pool, once inside the
transaction that picks the winner. Everything else in this spec — voids, the lock, the refusals —
exists to keep that function's answer true and the record of it complete.

Settled elsewhere and not re-argued here: ADR-0022 (the server picks, the list freezes at `start`),
`00` (the four tables, the state machine, the prize axis derived from draws, the draw taking no body,
`RandomNumberGenerator`, `eligible_pool_size`, the event gating the front half only), `01` (the frozen
`raffle_attendee` table and what `qualified` means). The evidence trail — redemption codes, handover,
the acta — is `03`.

**Amended 2026-10-04 (PGTI-119 grilling): the draw phase locks the raffle row, it does not stamp
it.** ADR-0028. `draw`, `void`, `reopen` (§6), `03`'s `handover`, `handover/reverse` and `complete`
each take `FOR UPDATE` on the `raffle` row first, then re-read and decide; none of them touches
`raffle.updated_at`. The raffle's `xmin` is for `PATCH` only. Everything below that said "stamp" now
reads "lock", and the races answer domain codes instead of `CONCURRENCY_CONFLICT`. The same session
settled four smaller points, written into their sections:

- The winner snapshot's id is `winner_attendee_id`, a restricting FK to `raffle_attendee.id`;
  `winner_name` and the eligible read's `displayName` are `"{first_name} {last_name}"`, trimmed.
  `00` §1's `winner_participant_id` is that column. §2.
- `PRIZE_ALREADY_DRAWN` carries the live draw in the response's draw shape: no email, no redemption
  code. The draw's response carries no redemption code either until `03`'s steps expose it. §7.1.
  *(PGTI-121 exposed it, so both now carry the code.)*
- `GET …/eligible` on a prize with a live draw is `409 PRIZE_ALREADY_DRAWN`. §5.
- The four void columns are created with `prize_draw` in BUILD-ORDER step 8, since its partial index
  and the pool's first two lines read them; step 9 adds the verb.

**Amended 2026-10-04 (PGTI-120 grilling).** `reopen` also clears `awards_batch_id`, leaves
`updated_at` alone (ADR-0028) and answers `200` with the `GET …/raffle` shape (§6). A void's `note`
is trimmed, and a blank one is stored as `null` and counts as missing (§3). `void` is never retried
automatically (§9): its route names the prize, not the draw, so a stale retry could void a newer
winner. Carrying a `drawId` was considered and rejected for the same reason §7.1 rejected an
`Idempotency-Key`.

## Decisions behind this shape

Settled with the user on 2026-09-22 in a grilling session on PGTI-36. Two of them refine what `00`
and `01` wrote down rather than only filling gaps:

- **A void reason is a closed code plus a note**, not the single `void_reason` column `00` §1 and §3
  imply. §3.
- **`reopen` deletes the frozen snapshot.** `01` §4 calls `raffle_attendee` immutable from `start`;
  it is, except in the one window where nothing has been drawn from it. §6.

One was amended afterwards:

- **Amended 2026-10-16 (PGTI-262): any void reaches the whole raffle**, superseding Decision 15's
  per-prize no-show. Until then only an `ineligible` void reached past the prize it voided.
  §3.1.

Everything else answers the five questions the ticket left open: concurrency, idempotency, what the
endpoint returns, void/redraw/reset semantics per state, and error codes.

## 1. The eligible pool

For prize **P**, the pool is the frozen qualified participants, minus three sets:

```
raffle_attendee WHERE qualified
  MINUS anyone with a voided draw anywhere in the raffle      <- §3.1, any code
  MINUS anyone holding a live draw          IF one_prize_per_person
  MINUS anyone whose company matches a live draw's company   IF one_prize_per_company
  = the pool for P
```

**Only live draws count for the two toggles.** A voided winner did not take a prize, so a void
never makes a person or their company a "winner". Decision 15 — out of that prize, still eligible for
later ones — is superseded (§3.1): a voided winner is out of every later prize.

The pool is asked for **per prize**, because it is what that prize's draw picks from; the rules today
give every undrawn prize the same pool. That is why the read in §5 is under the prize.

### 1.1 Company matching

`company` is self-reported free text (`01` §4.1). For the toggle, two companies are the same when
they are equal after:

- trimming, and collapsing internal runs of whitespace to one space;
- case-insensitive, accent-insensitive comparison.

So `"Turismo  Pérez"` matches `"turismo perez"`, and `"MITUR"` does not match `"Mitur SRL"`. **No
suffix stripping, no fuzzy matching.** An exclusion somebody cannot explain to a notary is worse than
a missed one, and the toggle is off by default for exactly this reason.

**A blank or null company never collides.** Otherwise everybody who left the field empty is one
company, and only one of them can ever win.

The mock lowercases and trims (`mockSorteo.ts:694`). This is that, plus whitespace and accents.

### 1.2 The toggles can change while the raffle runs

*(Amended by PGTI-275:)* this section used to lock both toggles at `start` with a `409`, because the
acta would have stated one rule set for a raffle that ran under two. The way out it named is now built: each draw records the rules in force when it was drawn.

`00` §4 allows `PATCH` while `in_progress`, and the toggles are in that body (ADR-0009 wants the
complete representation). While `in_progress`, a `PATCH` may change either toggle, and the change
applies **from the next draw**. A `completed` raffle still answers `RAFFLE_COMPLETED`.

- **Each draw stores the two rules it ran under**, `one_prize_per_person` and
  `one_prize_per_company` on `prize_draw`. They are the values the draw read from the raffle under the
  raffle row lock (§4) and built its pool from, so a draw cannot record rules it did not use. A
  `PATCH` takes no lock, but a draw never writes the raffle row.
- **Nothing is retroactive.** Turning the person toggle off puts earlier winners back in the pool for
  the next draw; turning it back on takes them out again. Live draws that break a rule turned back on
  stand, with no warning and no rejection.
- **No change log.** Who flipped a toggle and when is not recorded; each draw's rules are the
  evidence, and the acta prints them (`03` §4.2 item 4).

## 2. The draw

```
POST /v1/events/{eventId}/raffle/prizes/{prizeId}/draw        raffles:draw      no body
```

In one transaction:

1. **Lock the raffle row** (§4) and load it. Refuse unless `in_progress`.
2. Load the prize. Refuse if it has a live draw (§7).
3. Compute the pool for P (§1). Refuse if empty (§2.1).
4. Pick one member with `RandomNumberGenerator.GetInt32(pool.Count)` over the pool **ordered by
   attendee id**, so the index means the same thing on every run.
5. Insert the `prize_draw`: the winner's snapshot, `drawn_at/by`, `eligible_pool_size = pool.Count`
   and *(amended by PGTI-275)* the raffle's two toggles as read in step 1, the rules this draw ran
   under (§1.2).
6. Commit.

The response is `200` with **the whole prize** in the shape `GET .../raffle` uses for one: its derived
state, and its draw history, live and voided, newest first. The winner is the live draw.

The whole prize rather than the winner alone, because the client then replaces one object instead
of merging a fragment, and because after a redraw the history *is* what the operator's screen shows:

```json
{
  "id": "…", "roundId": "…", "name": "Botella de ron", "state": "drawn",
  "draws": [
    { "id": "…", "winnerAttendeeId": "…", "winnerName": "María Pérez", "winnerCompany": "Hotel Sol",
      "drawnAt": "2026-10-03T20:17:04Z", "eligiblePoolSize": 2, "void": null },
    { "id": "…", "winnerAttendeeId": "…", "winnerName": "Juan Gómez", "winnerCompany": "Viajes Caribe",
      "drawnAt": "2026-10-03T20:14:31Z", "eligiblePoolSize": 3,
      "void": { "code": "absent", "note": null, "voidedAt": "2026-10-03T20:16:10Z" } }
  ]
}
```

Redemption fields are `03`'s and are omitted here.

### 2.1 An empty pool

`409 RAFFLE_POOL_EXHAUSTED`, and **no draw row is written**. The prize stays `undrawn`. A draw that
picked nobody did not happen, so there is nothing for the acta to record.

The error carries, through `BaseError.ProblemExtensions`, the counts that explain it:

```json
{ "code": "RAFFLE_POOL_EXHAUSTED", "participants": 12,
  "excludedByVoid": 1, "excludedByPerson": 11, "excludedByCompany": 0 }
```

Each count is the number removed by §1's lines **in order**, first match wins — `excludedByVoid` covers
the void line — so they sum to `participants`.
An operator who sees `excludedByPerson: 11` knows why, without asking anyone.

**The rules never yield.** Falling back to people who have already won when the pool runs dry would be
a rule change nobody chose. *(Amended by PGTI-275:)* an operator may still turn a toggle off with a
`PATCH` (§1.2), which the next draw records; the server never does it on its own. The operator's options are to leave the prize
undrawn — `complete` permits it and the acta says "N drawn, M not" (`00` §2) — or nothing.

## 3. The void

```
POST /v1/events/{eventId}/raffle/prizes/{prizeId}/void        raffles:draw
{ "code": "absent", "note": null }
```

| `code` | Meaning |
| ------ | ------- |
| `absent` | the winner is not in the room (Decision 15's no-show, whose per-prize rule is superseded, §3.1) |
| `declined` | present, and refuses the prize |
| `ineligible` | discovered not to be allowed to win: staff, a duplicate, anything qualification missed |
| `other` | none of the above; `note` is **required** |

`note` is optional free text, at most 500 characters, and required for `other`. It is trimmed; a
blank note is stored as `null` and counts as missing. A code outside the four, or `other` without a
note, is a `400`.

**A code, not free text**, for two reasons. The frontend renders Spanish from codes and never from
prose (root `CLAUDE.md`, errors). And the acta prints it.

The void sets `void_code`, `void_note`, `voided_at` and `voided_by_id` on the live draw, which stays.
The prize drops back to `undrawn`. The response is `200` with the whole prize, as §2.

**A void never redraws.** The operator may decide to leave the prize undrawn. Void and draw are two
presses.

**The code never changes eligibility, and every void takes the person out of the rest of the
raffle.** The code is recorded and printed; nothing branches on it. A reason that could put somebody
back into a pool would be a lever the notary cannot audit.

### 3.1 A void reaches the whole raffle

A MITUR employee slips through qualification and wins prize 3. The operator voids it `ineligible`.
The snapshot is frozen and cannot be edited, so under a per-prize rule that person would come up
again for prizes 4 to 20, to be voided again each time, in front of the room.

The same holds for `absent` and `declined`: a winner who is not in the room, or who refused prize 3,
would come up again for prize 4 and be voided again in front of the room. So any void, whatever its
code, removes the person from **every later draw in this raffle** (§1, the void line). The reason is
printed beside the void on the acta, so the exclusion is stated where the notary reads it.

A void changes pools only, never existing draws. With `one_prize_per_person` off, a person's live
draw on another prize survives the void and can still be handed over. And the company stays
eligible: the two toggles read live draws only, so a voided winner's colleague is still in the pool
under `one_prize_per_company`.

A separate "disqualify participant" verb on the frozen attendee was considered and rejected: a new
verb, a new column and a new acta line for something the void already records.

## 4. Concurrency

Two races, and the database constraints from `00` catch one of them:

| Race | Caught by |
| ---- | --------- |
| The same prize drawn twice at once | `00`'s partial unique index, one live draw per prize |
| **Two different prizes, same winner**, with `one_prize_per_person` on | nothing, without this section |

The second is the real one: both transactions read the same pool and both can pick María. The same
holds for the company rule and for §3.1. The rules span prizes, so the serialisation has to be **per
raffle**.

**Every `draw` and every `void` locks the `raffle` row** (`SELECT … FOR UPDATE`, the
`LockRaffleAsync` that `start`, rounds and overrides already use) before it reads anything, in its own
transaction. Void is included because a void changes the pool too. Neither touches the raffle row
otherwise, so a draw never invalidates a `PATCH`'s `If-Match`.

The second of two racing requests waits, then reads the committed state and answers as it would have
alone: a draw on another prize finds the updated pool and draws from it, or answers
`RAFFLE_POOL_EXHAUSTED`; a draw on the same prize answers `PRIZE_ALREADY_DRAWN`. The partial unique
index stays as the backstop.

*Superseded 2026-10-04 (ADR-0028):* this section first stamped the raffle row so its `xmin` covered
the write, and rejected `FOR UPDATE` as a second idiom. The service already used `FOR UPDATE` on that
row, the stamp turned races into `CONCURRENCY_CONFLICT` or `DB_CONFLICT` by flush order, and a stamped
verb cannot see a locked one.

## 5. The eligible read

```
GET /v1/events/{eventId}/raffle/prizes/{prizeId}/eligible
```

```json
{ "count": 3,
  "participants": [
    { "attendeeId": "…", "displayName": "María Pérez", "company": "Hotel Sol" }, … ] }
```

The mock's execution screen feeds its client-side `elegibles` into the roulette: the names it spins
through, the "N participantes elegibles" line, and the disabled button at zero
(`SorteoEjecucionPage.tsx:68, 154, 345`; `RuletaAnimacion.tsx:693`). The client can no longer compute
that list — the rules now include voids, §3.1 and §1.1, and a TypeScript copy of them is how
they drift. So the server serves it, computed by **the same function** the draw calls.

Names and company only. No email: the list is for a projector.

**The draw never trusts this read.** It recomputes inside its own transaction. If the pool moved in
between, the animation briefly showed a stale list, which is harmless, and `eligible_pool_size` on the
draw is the truth.

Only while `in_progress`; otherwise `409 RAFFLE_NOT_IN_PROGRESS`. In `draft` there is no frozen pool
to answer from, and `01` §8's attendee list is the read for that. On a prize with a live draw it is
`409 PRIZE_ALREADY_DRAWN`, like the draw: no draw can follow, so no list would match what happens
next. No policy beyond the authenticated
fallback, like every other raffle read (`00` §10).

Embedding a pool per prize in `GET .../raffle` was rejected: N pools on every read, all but one
unwanted.

## 6. `reopen`, and there is no reset

The mock's `resetearSorteo` (`mockSorteo.ts:591-627`) wipes every winner, handover and signature. That
cannot exist here: every draw happened in front of a notary and two witnesses, and a reset erases
exactly what the acta records.

The mistake a reset legitimately fixes is narrower: **`start` pressed too early.** At 19:00, half the
room at the door, the drum freezes with 90 people instead of 400, and Decision 7 keeps the rest out.

```
POST /v1/events/{eventId}/raffle/reopen        raffles:draw      no body
```

`in_progress` → `draft`, permitted **only while the raffle has no draw rows at all, live or voided**.
It deletes the `raffle_attendee` rows and clears `started_at`, `started_by_id` and `awards_batch_id`.
It does not touch `updated_at` (ADR-0028), and answers `200` with the `GET …/raffle` shape, as `start`
does. Overrides survive,
because the freeze never deleted them (`01` §4.2), so the operator's decisions are all still there for
the next `start`.

- With any draw row: `409 RAFFLE_HAS_DRAWS`. From the first draw on, every correction is a void,
  and a void leaves a trace.
- **No trace of the reopen is kept.** Nobody was drawn; there is nothing to attest to.
- Gated by the event like `start` (`00` §5): on a `closed` or `cancelled` event it is
  `409 EVENT_NOT_EDITABLE`. A reopened raffle could never start again there, whereas one left
  `in_progress` can still `complete`.
- Serialised by locking the raffle row, like §4, so a `reopen` racing a first `draw` either runs
  first, or waits and answers `RAFFLE_HAS_DRAWS`.

The frontend's "Resetear sorteo" button maps to `reopen`, disabled once anything has been drawn.

## 7. What each prize state permits

While the raffle is `in_progress`. In `draft` both verbs are `409 RAFFLE_NOT_IN_PROGRESS`, and in
`completed` they are `409 RAFFLE_COMPLETED` (`00` §4).

| | `undrawn` | `drawn` | `redeemed` |
| --- | --- | --- | --- |
| `draw` | yes | `409 PRIZE_ALREADY_DRAWN` | `409 PRIZE_ALREADY_DRAWN` |
| `void` | `409 PRIZE_NOT_DRAWN` | yes | `409 PRIZE_ALREADY_REDEEMED` |

**There is no `redraw` verb.** A redraw is an ordinary `draw` on a prize that is `undrawn` again. §1
already makes the pool right, and a second verb is a second path through the same logic.

**A redeemed prize cannot be voided.** Somebody has signed for it and it has left the building;
voiding it would make the acta say it went to nobody. A handover recorded against the wrong person
is `03`'s problem, not a void.

**No order is enforced.** Any `undrawn` prize may be drawn at any time, whatever its round. The big
prize moves up because the sponsor has to leave, and a surprise prize (`00` §4) must be drawable the
minute it is added. Order does change outcomes under `one_prize_per_person`, and that is recorded
rather than controlled: every draw carries `drawn_at`, and the acta lists the draws in the order they
happened.

### 7.1 Idempotency: the prize is the key

The case that matters: the server commits María and the response is lost. The client retries.

Drawing a prize that already has a live draw is `409 PRIZE_ALREADY_DRAWN`, and the error **carries
the live draw** in its `ProblemExtensions`, so a retry recovers the winner without a second call. The partial
unique index is what makes this safe, and it already exists. The draw it carries has the shape §2's
response uses: no email. *(Amended by PGTI-121:)* since `03` §3.4 exposed it, that shape carries the
redemption code, the draw's `status` and its handovers, and so does this error.

- An `Idempotency-Key` header was rejected: a new column and a replay path for what one live draw per
  prize already guarantees.
- Answering `200` with the existing draw was rejected: an operator who forgot the prize was drawn
  would watch the roulette "win" somebody announced ten minutes ago.

The hole is **draw → lost response → human void → the stale retry lands and redraws unasked**. It
needs a client that retries automatically across a human action. The handoff (§9) forbids that.

## 8. Endpoints and error codes

Additions to `00` §10, all under `v{version:apiVersion}/events/{eventId:guid}/raffle`:

| Route | Role | Notes |
| ----- | ---- | ----- |
| `GET "prizes/{prizeId:guid}/eligible"` | — | §5. `in_progress` only |
| `POST "prizes/{prizeId:guid}/draw"` | `raffles:draw` | §2. No body. `200` with the prize |
| `POST "prizes/{prizeId:guid}/void"` | `raffles:draw` | §3. `{ code, note }`. `200` with the prize |
| `POST "reopen"` | `raffles:draw` | §6. Zero draw rows only |

| Code | Kind | When |
| ---- | ---- | ---- |
| `RAFFLE_POOL_EXHAUSTED` | Conflict | draw with an empty pool. Carries the counts, §2.1 |
| `PRIZE_ALREADY_DRAWN` | Conflict | draw on a `drawn` or `redeemed` prize. Carries the live draw, §7.1 |
| `PRIZE_NOT_DRAWN` | Conflict | void on an `undrawn` prize |
| `PRIZE_ALREADY_REDEEMED` | Conflict | void on a `redeemed` prize |
| `RAFFLE_HAS_DRAWS` | Conflict | `reopen` after anything was drawn |
| validation | Validation | void `code` outside the four, `other` without a note, `note` over 500 |

`RAFFLE_NOT_IN_PROGRESS`, `RAFFLE_COMPLETED` and `EVENT_NOT_EDITABLE` are `00`'s and are reused.

## 9. English ↔ Spanish, and the frontend handoff

| Backend | Mock's Spanish |
| ------- | -------------- |
| `void` | `declararPremioDesierto` |
| `reopen` | `resetearSorteo` — narrowed, see §6 |
| `GET …/eligible` | `elegibles` |
| `absent` / `declined` / `ineligible` / `other` | — (the mock records no reason) |

Per Decision 26 a backend developer may wire this and **may not change any aspect of the UI**. What
the frontend owner has to absorb:

1. **The roulette reads `GET …/eligible`** for its names and count. `useSorteoById`'s `elegibles`
   (`mockSorteo.ts:675-704`) goes.
2. **The draw's response is the winner.** The roulette animates toward it; nothing is picked in the
   browser.
3. **`draw` and `void` are never retried automatically.** On a lost response, refetch the prize.
   §7.1. A retried void that lands after a new draw would void the new winner.
4. **Void takes a code and an optional note**, and the prize comes back `undrawn` with the voided
   draw still in its history. Nothing is wiped.
5. **"Resetear" becomes `reopen`**, disabled once any draw exists.
6. *(Amended by PGTI-275:)* **The two toggles stay editable while `in_progress`** and apply from the
   next draw; each draw in the prize shape carries `onePrizePerPerson` and `onePrizePerCompany`.
7. **Draw and void never answer `CONCURRENCY_CONFLICT`** (§4). On any `409`, refetch and branch on
   the code.

## Definition of done

- The pool function is unit-tested line by line against §1: a winner voided on P with any code is out
  of P and out of Q; their company colleague stays in under the company toggle; a live winner is
  out under the person toggle and in without it; the company toggle removes `"turismo perez"` for a live
  `"Turismo  Pérez"`, keeps `"Mitur SRL"` for a live `"MITUR"`, and never removes a blank company.
- `RAFFLE_POOL_EXHAUSTED` writes no `prize_draw` row, and its four counts sum to `participants`.
- *(Amended by PGTI-275:)* `PATCH` while `in_progress` with a toggle changed succeeds and the read
  shows the new value. With the person toggle turned off after a draw, that draw's winner is in the
  next prize's `eligible`, and each draw in `GET .../raffle` carries the rules it ran under.
- **The cross-prize race**: two concurrent draws on two different prizes, with a pool of one and the
  person toggle on, produce exactly one draw and one `409 RAFFLE_POOL_EXHAUSTED`. This is the
  assertion the partial unique index does not give for free.
- A `draw` and a `void` racing in one raffle never answer `CONCURRENCY_CONFLICT`: each answers as it
  would have run alone after the other.
- A draw on a `drawn` prize is `409 PRIZE_ALREADY_DRAWN` and the problem body contains the live
  draw's winner.
- Void on `undrawn` is `PRIZE_NOT_DRAWN`, on `redeemed` is `PRIZE_ALREADY_REDEEMED`; neither changes a
  row. A void leaves the draw row in place with all four void columns set.
- `other` without a note, and an unknown code, are both `400`.
- `GET …/eligible` and the draw call one pool function — assert by test that the eligible read's
  `count` equals the next draw's `eligible_pool_size` when nothing moves in between.
- `GET …/eligible` returns no email field; assert the absence on the response type.
- `reopen` with zero draws returns the raffle to `draft`, deletes every `raffle_attendee` row, keeps
  every override, and a following `start` freezes the current attendance. With one voided draw and no
  live one, it is `409 RAFFLE_HAS_DRAWS` — the voided case is the one that regresses.
- `reopen` on a `closed` event is `409 EVENT_NOT_EDITABLE`.
- No `redraw` route, no reset route, and no `Idempotency-Key` handling exist in the raffle endpoints.
  Assert the absences.
