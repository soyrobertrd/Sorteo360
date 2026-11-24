# ADR-0022 — A raffle is an event sub-resource, and its participant list freezes when it starts

- Status: Accepted
- Date: 2026-09-20
- Makes a narrow exception to: ADR-0013 (consumers store ids, never names)
- Amended: 2026-09-21, see **Amendments** below

## Amendments

**2026-10-01 — the awards upload is deferred; raffles run with the awards step skipped.** Settled
in the PGTI-115 planning session. The 2026-09-29 amendment below kept the awards upload unchanged;
it is now postponed rather than dropped. Unlike attendance, the real source is external with no
date, so this is not "wait for the real thing": it is accepting that, until PGTI-115 ships, no
raffle applies the DO Travel Awards filter. `docs/specs/raffles/01-qualification.md` §5.2 already
makes that state loud — a warning on the read, `awards_batch_id` null on the frozen raffle, a line
on the acta — and that is what every raffle shows in the meantime. `IAwardsMembership` moves to
PGTI-116 with a registered implementation that always answers "no list in force"; the upload, its
two tables and the spreadsheet reader stay with PGTI-115.

**2026-09-29 — attendance has no stub; a placeholder that refuses stands in instead.** Settled in
the PGTI-114 grilling session; `docs/specs/raffles/01-qualification.md` §7. The Decision below says
each seam has "a stub behind it". For attendance that is no longer true. The stub was an upload of
the door list under the raffle, and it was dropped for two reasons. Its premise was stale: check-in
was blocked behind PGTI-14, which is now done, and check-in (PGTI-127 → PGTI-130) ships before any
real event hosts a raffle, so the upload would only ever have carried rehearsal data. And at a real
event it would have let people into the drum unchecked, since an uploaded row carries no `origin`
and no CRM contact, so the walk-in and blacklist gates could not have run on it.

Until PGTI-130 wires the real read, the host resolves `UnavailableAttendanceSource`, which fails
every call with `503 ATTENDANCE_UNAVAILABLE`. It is not a no-op: a draft read reports attendance as
`unavailable` and `start` refuses, both already specified for an attendance outage. The raffle is
built and tested against a fake source in the meantime, with every gate enforced from the start.
Gone with the stub: the attendance route, the `attendance_entry` table, `attendance_source` on the
raffle and its frozen rows, the warning and the acta line that reported it, and the `no_email`
reason, which only guarded against a non-portal source. The awards seam keeps its upload unchanged.

**2026-09-26 — referred people are eligible, and the walk-in reason below is out of date.**
Settled in the PGTI-39 grilling session; `docs/specs/raffles/01-qualification.md` §2 and §3.2 and
`docs/specs/convocatorias/09-check-in.md` §7. A walk-in is a registrant whose `origin` is `walk_in`.
A person who registered through a forwarded invitation (`referred`) is **in**. Item 1's structural
reason no longer holds, because every self-registration now creates a CRM contact; the rule stands
on MITUR's practice alone. The blacklist is asked about the registrant's own contact, and a
registrant with no CRM contact is not qualified and cannot be manually included.

**2026-09-21 — the qualification pipeline gains two steps, and one word is split in two.**
Settled in the PGTI-35 grilling session and specified in `docs/specs/raffles/01-qualification.md`.
These change what this ADR decided rather than refining it, so the original text below is left
standing and corrected here.

1. **Walk-ins are excluded from raffles.** Confirmed with Garry. MITUR takes walk-ins at public
   events; they do not go in the drum. The reason is structural as much as editorial: a walk-in
   registers without an invitation (`convocatorias/06` §1.1), so there is no `CampaignRecipient`
   and therefore no CRM contact, and rule 2 below cannot be evaluated on them at all. Admitting
   them would leave the blacklist reaching only the formally invited — the population least likely
   to need it.
2. **CRM's `do_not_invite` blacklist excludes, and sits *above* the human step.** The pipeline
   below says the manual include/exclude "overrides everything above". It no longer overrides
   everything: a person MITUR has decided must never be invited cannot be manually added to a
   drum. Only `do_not_invite` excludes — `opted_out` does **not**, because unsubscribing from
   campaign email says nothing about attending or winning.
3. **`attendee` and `participant` are now different words.** This ADR says "participant snapshot"
   and means the drum. The frozen table holds everyone who was at the door, qualified or not, so
   an attendee is anyone checked in and a participant is an attendee who qualified. The sentences
   below stay true under that reading; the table is named `raffle_attendee`.

The corrected pipeline:

```
checked in at the event
  MINUS walk-ins                       <- un-overridable
    INTERSECT DO Travel Awards members
    MINUS email domain ending in .do
    PLUS/MINUS manual include/exclude  <- human final say
  MINUS CRM do_not_invite              <- un-overridable
  = the drum
```

Two further points settled there refine rather than amend, and are recorded in the spec: the
awards seam is scoped **globally** rather than per raffle, since membership is a property of a
person in an external system; and **nothing is materialised before the freeze**, so the
"recomputation" the text below describes has no stored result to act on.

## Context

Raffles were excluded by decision on 2026-07-01 and recorded as out of scope in
`docs/ARCHITECTURE.md` §8. That exclusion was about sequence, not about merit: the feature was
always intended for a later phase. On 2026-09-20 the client's need arrived and the exclusion was
lifted. It is not a change of mind, and the registry entry has been corrected rather than argued
with.

Meanwhile the frontend built the whole feature against an in-memory mock — 6,666 lines across
twenty files under `frontend/src/features/sorteo/`, with rounds, prizes, a notary and two
witnesses, digital signatures, redemption codes and a browser-generated notarial record. So the
shape of the feature is not speculative; what is missing is where it lives and what it is allowed
to depend on.

Three questions had to be answered together, because each one's cheapest answer depends on the
others.

**Where does it live?** Three candidates, none obviously right. `events-service` owns the event a
raffle hangs off, and already holds the platform's only live service-to-service edge (to
`crm-service`, ADR-0013). `convocatorias-service` owns registrations, which is where attendance
comes from — but its `GLOSSARY.md` deliberately bans `Attendee`, `Guest` and `Attendance` as
identifiers, because it is a campaign service and was kept narrow on purpose. A new
`sorteos-service` is the textbook answer and the most expensive one: a fifth .NET container, its
own database and Keycloak client, entries in two compose files, on an on-premises stage host whose
memory is already being tuned down to fit (PGTI-24, PGTI-6).

**Who is in the draw?** The client's current manual process is: take the list of people checked in
to the event, keep those in the DO Travel Awards system, drop anyone whose email domain ends in
`.do`, then review the result by hand and add or remove individuals — including, when the list
comes out too small, people the automatic rules had excluded.

**What does the document say?** A raffle ends in an acta notarial signed by a notary and two
witnesses. That document states how many people were in the draw and who won.

Two of those dependencies do not exist. `convocatorias-service` has no registration at all — no
table, no QR, no reference code, no check-in — and the work is blocked behind PGTI-12 → PGTI-13 →
PGTI-14, which itself waits on PGTI-8, the client's public HTTPS front door with weeks of external
lead time. The DO Travel Awards system is not yet reachable either. Designing around their absence,
rather than waiting for them, was a constraint on this decision rather than an afterthought.

## Decision

**A raffle is a sub-resource of an event in `events-service`, and an event has at most one.**

Rounds inside the raffle do the grouping the client asks for — institutional prizes in one, large
experiences in another. One event therefore produces one participant list, one freeze moment, and
one acta, which is what the notary signs.

**The participant list is live while the raffle is in setup and freezes the instant the raffle
starts.** Whoever is checked in at that moment is in the draw; a late arrival is not. Starting the
raffle *is* the refresh, so there is no separate refresh action and no window in which the screen
and the document can disagree.

**The frozen list stores names, not only ids.** This is a deliberate exception to ADR-0013, which
says a consumer stores ids and resolves display text at read time. An acta must be legible and
defensible years later, when the person may have been renamed, soft-deleted or erased. Resolving a
legal record at read time makes it a view of current data rather than a record of what happened.
The exception is bounded to the raffle's participant snapshot and its prize winners; everything
else in `events-service` follows ADR-0013 unchanged.

**Qualification is a pipeline, computed server-side, with the human last:**

```
checked in at the event              <- the only way in
  INTERSECT DO Travel Awards members
  MINUS email domain ending in .do
  PLUS/MINUS manual include/exclude  <- overrides everything above
  = the draw
```

> **Superseded by Amendment 1 and 2 above.** Two steps were added and the manual step no longer
> overrides everything. See `docs/specs/raffles/01-qualification.md` §2.

Both intermediate lists are kept — qualified and not qualified — each excluded person carrying the
reason. Every person in both lists is checked in; the manual step moves people between them and
never admits an outsider. A manual decision is stored against the person with who made it and when,
and **survives recomputation**: twenty more people checking in must not silently undo a judgement
somebody made an hour earlier.

**The two absent dependencies are reached through seams**, each with a stub behind it:

- **Attendance.** The raffle asks a seam who is checked in. Its first implementation is a stub; the
  real read against `convocatorias-service` lands last, through the chassis `AddServiceClient`.
- **DO Travel Awards membership.** The raffle asks a seam whether an address is a member. Its first
  implementation is a list of addresses held in our own database and loaded by upload; the real
  integration replaces that one class.

This is the same shape as ADR-0014's storage seam, and it is what lets the feature be built,
tested and demonstrated before either dependency exists.

**The exclusion of Dominican domains is a hardcoded business rule, not configuration.** A suffix
match on `.do` also catches `.com.do`, `.gob.do` and `.edu.do`. Exceptions happen through the
manual include, which is recorded and visible on the acta; a configuration flag would let the rule
be turned off invisibly.

## Consequences

- `events-service` gains a substantial new bounded context, and it is already the largest service
  with its own tech-debt epic (PGTI-30). This is the real cost of the decision and is accepted
  knowingly: the alternative spends a container and a database to avoid it.
- The raffle can be built end to end before check-in exists. Only the final two chunks — replacing
  the attendance placeholder with the real read (PGTI-130), and the check-in endpoint itself
  (PGTI-127) — wait on check-in. *(Corrected 2026-09-29: this said "swapping the attendance stub"
  and "blocked on PGTI-14"; there is no stub, and PGTI-14 is done.)*
- The acta and the handover signature become stored files behind the existing file seam, each with
  a `storedFileId`, rather than artefacts the browser regenerates. A document a notary signs must
  not be rebuilt from live data every time it is opened.
- Generating the acta server-side requires a PDF library that the platform does not currently have.
  That is a root `Directory.Packages.props` decision, since package versions are governed there.
- The winner is chosen by the server, not the browser. The API returns a decided result and the
  roulette animates towards it. The mock's client-side `Math.random()` could never have been the
  source of truth for a notarised draw.
- A participant snapshot duplicates name, company and email into `events-service`. Anyone reading a
  raffle row in isolation will see data whose owner is elsewhere, which contradicts the platform's
  usual instinct; the exception is stated here so it is found rather than rediscovered.
- Two new function roles are needed, `raffles:write` and `raffles:draw`, and check-in will need
  `check-in:write` plus a narrow composite for door staff. Per the platform rules each lands twice:
  in the realm JSON, and as a console step in `docs/deploy-with-dokploy.md`.
- Domain names are English platform-wide (ADR-0008): `Raffle`, `RaffleRound`, `Prize`,
  `redemptionCode`, `winner`. The frontend's Spanish — `sorteo`, `ronda`, `premio`, `canje` — is
  presentation and stops at the UI.

## Alternatives rejected

**A new `sorteos-service`.** The cleanest boundary, and the honest answer if the platform were
bigger. Rejected on cost that is concrete rather than theoretical: a fifth .NET container, database
and Keycloak client on an on-premises host whose 16 GB already carry dev, stage, Dokploy and
Nextcloud, plus a synchronous hop for participants during a live draw in front of a room. The
frozen-snapshot decision removes the hot dependency that would have justified the separation.

**`convocatorias-service` owns the raffle.** Tempting, because it owns registrations and will own
check-in, so participants would never cross a service boundary. Rejected because that service was
deliberately scoped to campaigns and registration — its own `GLOSSARY.md` bans the attendee
vocabulary a raffle is made of — and because a live draw with prizes, notaries and signed handovers
is a different concern from sending email and taking sign-ups.

**A live participant list instead of a frozen one.** Simpler, no snapshot table, always current.
Rejected because it cannot produce a stable document: the acta's participant count would change
after it was signed, and a later deletion would quietly remove a name from a legal record.

**Recomputation wins over manual overrides.** Simpler to reason about, and keeps one source of
truth. Rejected because the client's actual process ends with a human review whose entire purpose
is to override the automatic rules — discarding that at the next recompute would delete the most
deliberate decision in the pipeline, silently, at the worst moment.

**Configurable excluded domains.** Rejected as described above: it converts a business rule into a
switch, and the legitimate escape hatch already exists and leaves a trace.

## Scope

This governs the raffle in `events-service` and the attendance contract it consumes. It does not
change how quotes, events or attachments behave, it does not alter ADR-0013 for any other consumer,
and it does not decide the check-in endpoint's own shape — that is specified separately in
`docs/specs/convocatorias/`. *(Corrected 2026-09-29: this said the endpoint "is blocked on
PGTI-14"; PGTI-14 is done.)*
