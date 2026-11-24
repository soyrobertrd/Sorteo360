# Raffle Qualification — Spec

Who is in the drum, how they got there, and what is written down the instant the raffle starts.

The pipeline has six steps and two of them cannot be overridden by anybody. Everything above the
manual step is advisory — it produces a *reason* — and the manual step produces the *outcome*,
except where a rule sits above the human on purpose.

Settled elsewhere and not re-argued here: ADR-0022 (where the raffle lives, the freeze, the
snapshot exception to ADR-0013, the two seams), ADR-0013 (consumers store ids and degrade),
ADR-0007 (in-memory cache per instance), ADR-0008 (English domain names). The raffle, its rounds,
its prizes and its state machine are `00`. The draw is `02`, the evidence trail is `03`.

## Decisions behind this shape

Settled with the user on 2026-09-20 and 2026-09-21 in a grilling session on PGTI-35. **Three of
them change ADR-0022 rather than refine it**, and the ADR carries an amendment block saying so:

- **Walk-ins are excluded from raffles entirely.** Confirmed with Garry on 2026-09-21. §2.
- **The CRM blacklist sits above the human step**, not below it. §3.
- **`attendee` and `participant` are different words now.** §1.

Two more depart from what was written down before:

- **Decision 11's awards seam is scoped globally**, not per raffle. §5.
- **Nothing is materialised before the freeze.** The Decisions of Record repeatedly say
  "recompute"; there is no stored result to recompute. §4.

**Amended 2026-09-26 by PGTI-39** (`convocatorias/09-check-in.md` §7), which specified the real
attendance read:

- **Referred people are eligible.** A walk-in is `origin = walk_in`, not "no
  `campaign_recipient_id`": a person who registered through a forwarded invitation carries the
  forwarder's recipient id and was counted as invited by accident. That outcome is now the rule on
  purpose. §2.
- **The blacklist is asked about the registrant's own CRM contact**, and a registrant with no CRM
  contact is not qualified, `contact_unresolved`. §3.2.

**Amended 2026-09-29 by PGTI-114**, which dropped the attendance stub. Check-in (PGTI-127 → PGTI-130)
ships before any real event hosts a raffle, so an attendance upload would only ever have carried
rehearsal data, and at a real event it would let walk-ins into the drum unchecked. Until PGTI-130 the
seam is a placeholder that refuses every call (§7.1). What went with the stub:

- **`attendance_source`** — the raffle column, the `attendance_source_uploaded` warning,
  `attendanceSource` on the read and the acta line *sin validación en puerta*. One source can only
  ever hold one value.
- **`no_email` and `PARTICIPANT_NOT_IDENTIFIABLE`.** The upload was the "future non-portal source"
  that reason defended against, and the portal requires an email. Six exclusion reasons remain. §6.
- **The dormant phase.** `walk_in`, `contact_unresolved` and `do_not_invite` are enforced from the
  start, built and tested against a fake attendance source.
- **`ATTENDANCE_UNAVAILABLE` and `CRM_UNAVAILABLE` are `503`, not `409`.** An outage, not a state
  conflict, matching the CRM directories and convocatorias' `CRM_UNAVAILABLE`. §8, §9.

**Amended 2026-10-01 by PGTI-115 and PGTI-116.** The awards upload (PGTI-115) is deferred, so until
it ships every raffle runs with the awards step skipped (§5.2) and ADR-0022 says so in its own
amendment. Planning PGTI-116 then settled five things this spec had left loose or got wrong:

- **The awards seam is one batched read that can say "no list"**, `GetInForceAsync`, and PGTI-116
  registers an implementation that always answers that. §5.
- **Attendance degradation is reported in the body, `attendanceStatus`, not on
  `X-Crm-Directory-Status`.** That header speaks for CRM alone, platform-wide, and attendance comes
  from convocatorias. §8.
- **Over the attendee ceiling, the raffle read stays up**: `attendance_too_many_rows` is a blocking
  reason there, and only the attendees read and `start` refuse. §8.1.
- **Unknown is not empty.** With no copy of the door list, `no_qualified_attendees` is not reported
  beside `attendance_unavailable`. §8.1.
- **Readiness is a draft's question.** Outside `draft`, `isReadyToStart` and `blockingReasons` are
  absent. §8.1.

**Amended 2026-10-02 in review of PGTI-116.** The attendee ceiling is now **5,000**, up from 1,000,
and it no longer follows CRM's batch cap. 1,000 had been chosen only because the mailability call
takes at most 1,000 ids. Nobody had decided it was a sensible size for an event, and a large public
event could exceed it. The mailability check is now sent in batches of at most 1,000 ids, one after
another, and the whole check fails if any batch fails (§3.1). Convocatorias already checks a
campaign's recipients this way. The ceiling stays as a sanity bound on one unpaged read of the door
list (§7).

**Amended 2026-10-03 by PGTI-117**, planning the overrides and the CRM gate. Seven points the spec
had left open:

- **No cache for CRM's answers.** Draft reads ask CRM every time; only the door list is cached.
  §8.
- **With CRM down, a draft read skips the blacklist step rather than blanking**, blocks on
  `crm_unavailable` and says so on `X-Crm-Directory-Status`. §8.
- **A contact id CRM does not return is `contact_unresolved`**, not `do_not_invite`: CRM has no
  record of the person, which is not the same as having blacklisted them. §3.1.
- **Override writes read attendance live** and fail closed, and an include asks CRM live about that
  one contact. §9.1.
- **Only an include is refused for walk-in, no contact and blacklist.** An exclude only needs the
  person at the door. §9.1.
- **`POST` upserts, `DELETE` is idempotent**, and overrides answer to the event gate of `00` §5.
  §9.1.
- **`overruled_reason` is pinned down**, and is computed on the draft read as well as frozen. §4.1.

**Amended 2026-10-04 by PGTI-190**, closing two holes in the CRM gate PGTI-117 built:

- **A suspended contact is out**, with its own reason `suspended`, above `do_not_invite` and above
  any human; a manual include is `409 PARTICIPANT_SUSPENDED`. CRM's suspension (PGTI-186) came after
  §3 was written, and the gate let it through as an unknown reason. §3, §6, §9.1.
- **The gate reads CRM's invitation answer, not the email one.** `suppressionReason` ranks
  `no_email` and `opted_out` above `do_not_invite`, so a blacklisted person who also opted out was
  reported as `opted_out` and qualified. The gate now reads `canBeInvited` and
  `inviteSuppressionReason` (`crm/03` §1b), which ignore the email state. Opted-out people still
  qualify. §3.
- **An unknown invitation reason excludes**, as `do_not_invite`. The old rule — an unknown email
  reason qualifies — is reversed, because the field now asks the right question. §3.

**Amended 2026-10-04 by PGTI-118**, planning `start` and the freeze. Six points the spec had left
open:

- **The frozen row copies the override's note**, as `manual_note`, so one read of the frozen table
  still answers everything without a join. §4.1.
- **`awards_batch_id` is a plain nullable column** until the awards upload creates the table it
  points at; PGTI-115 adds the foreign key. §4.1.
- **The frozen attendees read reports `attendanceStatus: "ok"` and `attendanceFetchedAt` equal to
  `started_at`.** The list was read live at that instant, and the raffle's state already says it is
  frozen; a new status value would only be one more for the client to handle. §8.
- **`awards_list_not_in_force` outlives the freeze.** Outside `draft` it is read from
  `awards_batch_id IS NULL`, so the operator sees all evening what the acta will state. §8.1.
- **Override writes are serialised on the raffle row**, like `start`, so an override can never
  land after the freeze it was meant to be part of. §9.1.
- **Every empty drum is `RAFFLE_NO_PARTICIPANTS`**: nobody at the door, everyone excluded, or an
  awards list matching nobody. §9.

## 1. Attendee and participant are not the same word

ADR-0022 says "participant snapshot" and means the drum. This spec freezes **everyone who was at
the door**, qualified or not, so one word would have had to mean both.

| Term | Means |
| ---- | ----- |
| **attendee** | a person checked in to the event. The frozen table is `raffle_attendee` |
| **participant** | an attendee who qualified. `qualified = true`, and nothing else |
| **the drum** | the participants, collectively. What `02` draws from |

So every participant is an attendee and most attendees are participants, and no row anywhere is a
participant who did not participate. ADR-0022's sentence stays true as written.

`convocatorias-service/GLOSSARY.md:81,117` bans `Attendee`, `Attendance`, `Guest` and `Registro` as
identifiers. **That ban is scoped to that service**, and ADR-0022 cites it as one of the reasons
the raffle is not there. Using the word here does not cross the boundary; it is on the other side
of it.

## 2. The pipeline

Six steps, evaluated in this order. ADR-0022 states four of them; the two `MINUS` steps at top and
bottom are added by this spec.

```
checked in at the event              <- the only way in
  MINUS walk-ins                     <- un-overridable
    INTERSECT DO Travel Awards       <- skipped entirely when no list is in force
    MINUS email domain ending .do    <- hardcoded (Decision 12)
    PLUS/MINUS manual include/exclude<- human final say
  MINUS no contact, suspended, or do_not_invite <- un-overridable (§3, §3.2)
  = the drum
```

**Identity is the normalised email**, trimmed and lowercased — the same conservative subset
`convocatorias/05` §5 uses, and for the same reason: any drift makes us under-merge, which is
visible, rather than over-merge, which silently fuses two people. No gmail dot-stripping, no `+tag`
handling. `contactId` rides along on the attendee but is **not** the identity; it exists solely as
CRM's handle for §3.

**Walk-ins are out.** A walk-in is an attendee whose `origin` is `walk_in` (`convocatorias/07` §3):
someone who registered for a public event without ever receiving an invitation
(`convocatorias/06` §1.1). MITUR takes walk-ins at public events and does not put them in raffles.
That practice is the whole of the rule. An earlier draft also argued that a walk-in has no CRM
contact, so §3 could not be evaluated on them; that stopped being true when self-registration began
creating a CRM contact for every registrant (`convocatorias/06` §4.2).

**Referred people are in.** `origin = referred` is someone who registered through an invitation
somebody else forwarded: a travel agent passing the email to a colleague at the same agency. MITUR
wants that colleague in the room, and in the drum. They go through every other step like an invitee,
and the blacklist is asked about **them**, not about the colleague who forwarded the email (§3.2).

That gives the drum a one-sentence invariant an auditor can check:

> Every person in the drum was invited to this event, or registered through an invitation, and was
> cleared by CRM.

**A manual include cannot admit a walk-in.** `409 PARTICIPANT_WALK_IN`. Letting the human step
reach them would reopen the hole the rule exists to close, one person at a time, and that one
person would be the only one in the drum nobody checked.

## 3. The blacklist sits above the human, not below

CRM carries `do_not_invite_at` — MITUR's judgement that a person must never be invited. CRM never
exports that column; it exports the computed pair `canBeEmailed` + `suppressionReason`
(`crm/03-contacts-gdpr.md` §1a, `ContactMailability.cs`).

*(Amended by PGTI-190.)* **The gate reads CRM's invitation answer**, `canBeInvited` +
`inviteSuppressionReason` (`crm/03` §1b), not the email pair. `suppressionReason` answers "may we
email them", with one code by fixed precedence, so `opted_out` and `no_email` hid a `do_not_invite`
behind them, and `suspended` was not a reason the gate knew. The invitation pair is evaluated apart
from the email state:

| `canBeInvited` | `inviteSuppressionReason` | Are they in the drum? | `exclusion_reason` |
| -------------- | ------------------------- | --------------------- | ------------------ |
| `true` | `null` | yes, whatever `suppressionReason` says | — |
| `false` | `suspended` | no | `suspended` |
| `false` | `do_not_invite`, or any other value | no | `do_not_invite` |

**The gate is `canBeInvited`; the reason only names it.** A reason CRM adds later keeps people out
with no change here. A row missing `canBeInvited` is an unreadable answer, so the whole check fails
as `crm_unavailable`, never as a drum of everybody or of nobody.

The email table below still holds as a statement of what does **not** exclude. Its last row is now
reached through `inviteSuppressionReason`, whatever `suppressionReason` says.

**No email reason excludes on its own. `opted_out` does not.** *(Amended by PGTI-190: this read
"Only `do_not_invite` excludes"; exclusion now comes from `canBeInvited` alone, above.)*

| `suppressionReason` | May we email them? | Are they in the drum? |
| ------------------- | ------------------ | --------------------- |
| `null` | yes | yes |
| `no_email` | no | cannot arise: the portal requires an email (`convocatorias/06` §1) |
| `opted_out` | no | **yes** |
| `do_not_invite` | no | no |

Unsubscribing from campaign email is not a statement about standing in a room or winning a bottle
of rum. Gating on `canBeEmailed` wholesale would throw out everyone who ever clicked unsubscribe,
for a reason with nothing to do with prize draws — and would mean CRM adding a fourth suppression
reason silently shrinks drums with no code change anywhere.

This is a **read of published contract, not a reimplementation of CRM's rule**. CRM's own warning
in §1a is against recomputing the answer from `opted_out_at` and `do_not_invite_at`; nothing does
that here. `suppressionReason` is three named values CRM publishes deliberately, and the spec pins
the branch with a test so it cannot quietly widen back to all three (§10). *(Amended by PGTI-190:
the branch now reads `canBeInvited`, and the pinned test is that `opted_out` with `canBeInvited:
true` qualifies.)*

**The gate runs last, after the manual step.** ADR-0022's arrow says the human "overrides
everything above", and the whole point of a blacklist is that they do not override this. Ordering
it below the human is the only place it can sit and still mean what it says.

Two consequences:

- A manual include naming an already-blacklisted person is refused at write time,
  `409 PARTICIPANT_DO_NOT_INVITE`, and a suspended one `409 PARTICIPANT_SUSPENDED` (PGTI-190).
  **Nothing is written.**
- An override written *before* the person was blacklisted is **not deleted**. It simply loses at
  compute time, the person appears as not qualified with reason `do_not_invite`, and the snapshot
  records that a manual include was overruled. Deleting it instead would need something in
  events-service watching CRM for blacklist changes, and nothing does.

That second rule is what answers "the coordinator says she added María, so why is María not on the
acta".

### 3.2 Whose contact, and when there is none

**The contact asked about is the registrant's own**, `crm_contact_id` on their registration, as the
attendance read returns it (`convocatorias/09` §7.1). Never the contact of the CampaignRecipient
whose link they used: for a referred person that is somebody else.

**A registrant with no CRM contact is not qualified**, reason `contact_unresolved`. It happens when
the CRM write at registration failed and nobody has retried it yet (`convocatorias/07` §6.1). The
blacklist cannot be asked about a person CRM has no record of, and treating that silence as
clearance is §3.1's mistake in another form.

**It cannot be manually overridden**, for the same reason a blacklisted person cannot be:
`409 PARTICIPANT_CONTACT_UNRESOLVED`. An include would put in the drum the one person nobody checked.
The fix is upstream and quick: retry the CRM write from convocatorias' reconciliation screen, and the
next compute qualifies them.

### 3.1 How the question is asked

`POST /v1/contacts/mailability`, body `{ ids: [...] }`, with the distinct contact ids of the
attendees who have one. CRM takes at most 1,000 ids per call (`crm/03-contacts-gdpr.md:445`), so
the client splits the ids into batches of at most 1,000 and sends them **one after another**, then
joins the answers. That is up to five calls at the 5,000-attendee ceiling (§7). Calls go through
the chassis `AddServiceClient`, carrying events-service's own client-credentials token, as the one
existing edge already does (ADR-0013). Convocatorias' launch re-check
(`CrmContactDirectory.CheckMailabilityAsync`) works the same way.

- **One after another, not in parallel.** Parallel batches would multiply the load on a service the
  raffle already depends on, for a saving of milliseconds.
- **All or nothing.** If any batch fails, or answers with an empty body, the whole check fails:
  `crm_unavailable` on a draft read, and `503 CRM_UNAVAILABLE` on `start`. Keeping the batches that
  did answer is not an option, because of the rule below. Every id in the failed batch would come
  back with no answer, and the raffle would silently treat those people as blacklisted instead of
  reporting the outage.

**An id that does not come back is not cleared.** CRM's contract says a soft-deleted contact
simply has no row, and treating silence as clearance would readmit exactly the people CRM removed.
*(Amended by PGTI-117.)* Its reason is **`contact_unresolved`**, not `do_not_invite`: CRM has no
record of the person, which is §3.2's case, and telling a coordinator "blacklisted" would be false.
It is just as un-overridable, so a manual include is refused with `PARTICIPANT_CONTACT_UNRESOLVED`,
and an include written before the contact was deleted now loses to `contact_unresolved`.

## 4. Nothing is stored before the freeze

While the raffle is `draft`, both lists are **computed on every read** from the two seams plus the
stored overrides. No rows exist. `start` runs the pipeline once more and writes `raffle_attendee`,
immutable from that instant.

```
draft          GET .../attendees
                 └─ attendance seam + awards table + CRM + overrides
                    └─ computed, never written

               POST .../start                     <- the freeze
                 └─ pipeline once, live reads
                    └─ INSERT raffle_attendee (N rows), immutable

in_progress    GET .../attendees
                 └─ SELECT raffle_attendee        <- no seam call, ever
```

**This dissolves "what triggers a recompute": nothing does.** There is no stored result to go
stale, so there is no refresh button to forget to press, which is Decision 7 falling out of the
storage model instead of being enforced on top of it. A materialised pre-start list would be stale
the moment somebody new walked through the door, on the exact screen where an operator is deciding
who is in.

It also means `raffle_attendee` has exactly one meaning — the frozen drum and its rejects — and can
never be caught half-live. There is no `frozen_at` flag making one table mean two things across the
freeze; the raffle's own `started_at` (`00` §1) is the freeze time, and rows exist only after it.

### 4.1 What the freeze writes

One row per checked-in person, **qualified or not**:

| Column | Notes |
| ------ | ----- |
| `raffle_id` | |
| `email_normalized` | the identity. Not null |
| `contact_id` | CRM's handle. Null when the registrant's CRM write never landed (§3.2) |
| `first_name`, `last_name`, `company` | **names, not only ids** — ADR-0022's bounded exception to ADR-0013 |
| `checked_in_at` | from the attendance seam |
| `qualified` | boolean. True means a participant |
| `exclusion_reason` | null exactly when `qualified` |
| `manual_decision` | null \| `included` \| `excluded` |
| `manual_decided_by_id`, `manual_decided_at` | who and when, copied from the override |
| `manual_note` | the override's note, copied with it *(added by PGTI-118)* |
| `overruled_reason` | what the manual step beat, or what beat the manual step — see below |

*(Amended by PGTI-117.)* `overruled_reason` takes an `exclusion_reason` value, and is the same on
the draft read as on the frozen row:

| Case | `overruled_reason` |
| ---- | ------------------ |
| an include that wins | the reason the person would otherwise have had, first match in §6 order: a non-member with a `.do` address carries `not_awards_member` |
| an include that loses (blacklisted, or contact gone, after it was written) | the reason that beat it, equal to `exclusion_reason` |
| an include on someone who qualifies anyway | `null` |
| any exclude | `null`: an exclude either wins or is shadowed by a reason ranked above it, and `manual_decision` already says so |

Plus, on the raffle itself: `awards_batch_id` (null when no list was in force).

*(Amended by PGTI-118.)* `awards_batch_id` carries no foreign key yet: the table it names arrives
with the awards upload (PGTI-115), which adds the constraint. Until then the column is always null.

**`company` is self-reported free text** from the registration form, which is its only source
(`convocatorias/06` §1). `02`'s `one_prize_per_company` toggle has to live with that; this spec
records what the person typed and makes no attempt to reconcile "MITUR", "Mitur SRL" and "mitur".

**Both lists are frozen, not only the drum.** People keep arriving after the freeze, so a
not-qualified list recomputed later would grow all evening and the acta's arithmetic — 412 checked
in, 380 in the draw, 32 excluded — would stop adding up by the time anyone read the document. The
narrower alternative, freezing the drum plus per-reason counts, was rejected on one ground: it
cannot name an individual afterwards. The pipeline is not reproducible later, because the awards
list is replaced wholesale (§5) and CRM's blacklist moves, so "you were not in the draw because
your address ends `.do`" is unrecoverable unless the row exists. A few dozen extra rows per event
is not a volume decision.

### 4.2 Overrides survive, because they are the only thing that persists

`raffle_attendee_override` is written during `draft` and is the sole durable state before the
freeze:

| Column | Notes |
| ------ | ----- |
| `raffle_id`, `email_normalized` | unique together |
| `decision` | `included` \| `excluded` |
| `note` | optional free text, shown to the next operator. At most 1,000 characters; blank is stored as null |
| `decided_by_id`, `decided_at` | |

Decision 8's "manual overrides survive recomputation" is automatic here: there is no recomputation
to survive. Twenty more people checking in changes nothing an operator decided an hour earlier,
because the decision is a row and the computed list is not.

Overrides are **not** deleted at the freeze. They stay as the record of what was decided and by
whom, and `raffle_attendee` carries a copy of the provenance so a single read of the frozen table
answers the acta's questions without a join.

## 5. The awards seam

Decision 11: DO Travel Awards is unreachable, so it is a seam whose first implementation is a list
of addresses in our own database.

**One global list, not one per raffle.** Membership of DO Travel Awards is a property of a person
in an external system, not of an evening. The real API will answer "is this address a member"
globally, so a per-raffle list would make the seam's shape change when the real integration lands —
the precise thing ADR-0022 says seams exist to prevent.

```
IAwardsMembership.GetInForceAsync()  -> Maybe<AwardsList(BatchId, Members)>
  ├ until PGTI-115: NoAwardsList               -> always None: the step is skipped
  ├ PGTI-115:       UploadedAwardsMembership   -> awards_member
  └ later:          DoTravelAwardsClient       -> the real API
```

*(Amended by PGTI-116: the sketch was `IsMember(email)` / `Members()`.)* One read per compute rather
than one per attendee, and `None` is the seam saying it has no opinion — the state §5.2 treats as
"skip", which an empty set could not express. The batch id travels with the members it describes,
so the freeze records the list it actually used.

Two tables. `awards_upload_batch` is permanent, one row per upload, carrying `original_filename`,
`detected_encoding`, `row_count`, `accepted`, `uploaded_at`, `uploaded_by_id`. `awards_member` is
the membership itself, unique on `email_normalized`.

**An upload replaces the whole list in one transaction.** A membership roster is a snapshot, not a
log; appending would make the list monotonic, so somebody who left the programme could never be
removed. The freeze records `awards_batch_id`, so an acta can still name the list that was in
force even after three more uploads.

### 5.1 The upload

```
POST /v1/awards-members          raffles:write        multipart
```

**One endpoint, no staging table and no `confirm` step.** `convocatorias/05`'s elaborate two-phase
flow exists to protect irreversible `POST`s creating contacts in CRM. There is no such write here:
a wrong file is fixed by uploading the right one, and the previous list is gone either way.

xlsx and csv, because the client's export will be xlsx and forcing a Save-As-CSV by hand the
evening of an event is where mojibake comes from. What is required of the file is one thing: a
column of email addresses.

Borrowed from `05` §3 unchanged, as idioms rather than as code — that policy lives in
convocatorias and `05` §3.3 already refused to share it:

- xlsx: signature `50 4B 03 04` **and** a `ZipArchive` entry `xl/workbook.xml`, because docx, pptx,
  jar and apk all begin with those four bytes.
- csv: decode as strict UTF-8, fall back to Windows-1252 on `DecoderFallbackException`, and surface
  `detected_encoding` on the response so a mangled `José` has a lead beside it.
- 5 MB file cap, 100 MB decompressed budget for the zip bomb, counted while reading.

The response is the whole report; there is nothing to confirm:

```json
{
  "batchId": "…", "originalFilename": "premios-2026.xlsx",
  "detectedEncoding": "utf-8",
  "rowCount": 412, "accepted": 405,
  "invalidAddresses": 4, "duplicatesCollapsed": 3,
  "replacedPreviousCount": 380
}
```

**Row-level failures never fail the upload**, per `05` §4's reasoning: rejecting a 400-row file over
one typo is how people end up hand-editing spreadsheets at midnight. A file where every row is
invalid is still a successful upload with zero accepted addresses, which is how somebody discovers
they uploaded the wrong column.

### 5.2 No list in force is not an empty list

Two states, two answers:

| State | Pipeline | `start` |
| ----- | -------- | ------- |
| No batch has ever been uploaded | the awards step is **skipped** | allowed |
| A batch is in force, matching nobody | intersect yields nothing | `409 RAFFLE_NO_PARTICIPANTS` |

A seam with no data behind it has **no opinion**, and treating an absent list as an empty one would
make the most confusing possible symptom — zero qualified people, no reason given — the default
state of a freshly built feature. It would also make the raffle undemonstrable until the client
delivers their first file.

**The skip is never silent.** It appears three ways: as a `warnings` entry on the read (§8), as
`awards_batch_id = null` on the frozen raffle, and as a line on the acta. Decision 12 refused to
make the `.do` rule configurable because "a config knob would invite someone to quietly disable a
business rule"; the same objection applies here and is answered by making the skip loud rather than
by refusing to start.

## 6. Exclusion reasons

Ordered, **first match wins**, disjoint so the counts reconcile against the door — the same shape
CRM §1a and `convocatorias/05` §4 both use.

| # | `exclusion_reason` | Meaning | Overridable |
| - | ------------------ | ------- | ----------- |
| 1 | `walk_in` | `origin = walk_in`; never invited, not referred | **no** |
| 2 | `contact_unresolved` | no CRM contact, so the blacklist cannot be asked, §3.2 | **no** |
| 3 | `suspended` | CRM suspended the contact; MITUR does no business with them (PGTI-190) | **no** |
| 4 | `do_not_invite` | CRM blacklist | **no** |
| 5 | `manually_excluded` | a human took them out | n/a — it *is* the human |
| 6 | `not_awards_member` | absent from the list in force | yes |
| 7 | `dominican_domain` | address ends `.do` | yes |

*(Amended by PGTI-190.)* `suspended` ranks above `do_not_invite` because CRM's own order does
(`crm/03` §1b): a suspension includes the blacklist, so a person who is both is named for the
larger of the two.

```
checked_in = qualified + not_qualified          always, exactly
```

That identity is the point of listing rather than filtering. A screen that shows 380 when the door
counted 412, with nothing accounting for the other 32, is the complaint CRM §1a makes about a
consumer expecting 240 recipients and silently getting 227.

**`dominican_domain` is a suffix match on `.do`**, which also catches `.com.do`, `.gob.do` and
`.edu.do`. Hardcoded, named plainly in the code, not configurable — Decision 12, unchanged. The
legitimate escape hatch is the manual include, which is recorded and reaches the acta.

## 7. The attendance seam

Decision 24: the raffle reads attendance through a seam, so that only the last chunk waits on
check-in.

```
IAttendanceSource.GetCheckedInAsync(eventId)
  ├ until PGTI-130: UnavailableAttendanceSource    -> fails every call, 503 ATTENDANCE_UNAVAILABLE
  └ PGTI-130:       ConvocatoriasAttendanceSource  -> AddServiceClient
```

Each row carries `emailNormalized`, `firstName`, `lastName`, `company`, `contactId`, `origin` and
`checkedInAt`. They come from `GET /v1/convocatorias/events/{eventId}/check-ins`, specified in
`convocatorias/09` §7.

**The seam returns the whole door list, unfiltered** — walk-ins, `.do` addresses, blacklisted
people, everyone. Qualification classifies them. A seam that pre-filters buries a business rule in
the transport, and the reconciliation in §6 needs the full number.

One unpaged call, with a stated ceiling of **5,000 attendees** *(amended 2026-10-02; it was 1,000,
CRM's batch cap)*. The ceiling is a sanity bound on one unpaged read of the door list and on the
copy cached per event. It is no longer tied to CRM, since the mailability check is sent in batches
(§3.1). An event exceeding it is refused loudly rather than silently truncated — a truncated drum is
a wrong acta.
The ceiling is the raffle's, not the seam's: the caller counts and refuses with
`ATTENDANCE_TOO_MANY_ROWS`.

### 7.1 No stub

Until PGTI-130 the registered source is `UnavailableAttendanceSource`, in every environment. It is
not a no-op: it fails every call, so the draft read reports attendance `unavailable` and `start`
refuses with `503 ATTENDANCE_UNAVAILABLE` (§8), exactly as for an outage. Nothing can vanish behind
it. The raffle is built and tested against a fake source meanwhile, with every gate of §6 enforced.

**An attendance upload under the raffle was specified here and dropped on PGTI-114** (2026-09-29).
It would have made the feature rehearsable before check-in existed, which is all it was for. Two
things removed the reason to build it:

- **Its premise was stale.** Check-in was "blocked behind PGTI-14, which waits on PGTI-8". PGTI-14
  is done, PGTI-127 has no blockers, and check-in ships before any real event hosts a raffle. The
  upload would only ever have carried rehearsal data.
- **At a real event it would have been a hole.** An uploaded row carries what a coordinator can type
  — no `origin` and no `contactId` — so `walk_in`, `contact_unresolved` and `do_not_invite` could
  not have run on it. The gates would have been dormant, the acta would have had to say so, and a
  raffle frozen that way puts walk-ins in the drum unchecked.

## 8. Reads, caching and degradation

```
GET /v1/events/{eventId}/raffle/attendees        authenticated
```

Reads carry no policy, per `00` §10 and `QuoteQueryEndpoints.cs:41-43`. The response is both lists
with their reasons, plus the provenance an operator needs to trust what they are looking at:

```json
{
  "checkedIn": 412, "qualified": 380,
  "attendanceFetchedAt": "2026-09-21T19:42:03Z",
  "attendanceStatus": "ok",
  "awardsBatchId": null,
  "attendees": [ { "email": "…", "firstName": "…", "company": "…",
                   "qualified": false, "exclusionReason": "dominican_domain",
                   "manualDecision": null, "overruledReason": null,
                   "manualNote": null, "manualDecidedById": null, "manualDecidedAt": null } ]
}
```

*(Amended by PGTI-117: the four fields after `manualDecision`.)* The note is there because §4.2
promises it to the next operator; the decider is a raw id, since resolving names is the acta's
business (`03`), not the setup screen's.

**Draft reads go through `StaleTolerantCache`** (`Events.Infrastructure/Directories/`), whose rule
is already "a copy outlives its own validity": a failed refresh serves the previous copy rather
than erroring. A setup screen polls while people arrive, and a momentary outage must degrade rather
than blank it — ADR-0013, and the behaviour `CachedOptDirectory` and `CachedCompanyDirectory`
already have. Degradation is reported with the existing vocabulary, `ok` / `stale` / `unavailable`,
in the body as `attendanceStatus`, and `attendanceFetchedAt` gives the copy's age so an operator can
see they are looking at a list from forty seconds ago.

*(Amended by PGTI-116: this said `X-Crm-Directory-Status`.)* That header is one status for the whole
of CRM per request, platform-wide, and attendance is convocatorias'. Reporting it there would put
one service's outage under another's name, and from PGTI-117 the same response also carries a real
CRM status for the mailability call, which is what the header is for.

What the cache holds is the **door list only**, one copy per event: never the qualification result.
Awards, overrides and CRM's answers are applied on every read, so an operator's override shows at
once and only arrivals can lag. Fresh for `RaffleAttendance__CacheTtlSeconds` (default 30), kept
12 hours as a fallback — an evening, not the directories' 30 days, since this is a list of named
people at one event.

*(Amended by PGTI-117.)* **CRM's answers are not cached at all.** A realistic event is one
mailability call per read, five at the ceiling; a per-contact cache would add a `stale` state to
build and test, and let a fresh blacklisting lag by its TTL, to save calls nobody has measured as a
problem. It can be added later without changing the contract. Both `GET …/raffle` and
`GET …/attendees` compute the drum, so both ask.

**With CRM unreachable, a draft read classifies everyone except the blacklist step.** Rows that
would otherwise qualify show as qualified, `crm_unavailable` joins `blockingReasons`, and
`X-Crm-Directory-Status: unavailable` is sent on both reads — the header's own job, since this is
CRM's outage. Blanking the list like an attendance outage would empty the setup screen over a CRM
blip; marking the unanswered as excluded is §3.1's mistake. The count may include someone CRM would
have refused, which is harmless: `start` is blocked while the flag stands, and reads live anyway.
With CRM answering, the header is `ok`.

**`start` bypasses the cache entirely and reads live.** This is the one read that must be true
rather than merely recent: the freeze is irreversible, `00` §2 provides no transition out of
`in_progress`, and a drum minted from a stale copy cannot be corrected afterwards — nor can the
acta printed from it. ADR-0013's degrade-never-fail rule is about reads; this is a write that mints
a legal record, which is the case that rule was never about.

| | draft read | `POST start` |
| --- | --- | --- |
| attendance unreachable | degrade, flag `stale` / `unavailable` | `503 ATTENDANCE_UNAVAILABLE` |
| CRM unreachable | degrade, flag | `503 CRM_UNAVAILABLE` |
| awards (local table) | n/a | n/a |

*(Amended by PGTI-118.)* **Once the raffle has started, the attendees read is the frozen table**, in
the same order as the draft read, and calls no seam. It answers `attendanceStatus: "ok"` with
`attendanceFetchedAt` equal to `started_at`, because the list was read live at that instant, and
`awardsBatchId` from the raffle. No CRM is asked, so `X-Crm-Directory-Status` is absent.

### 8.1 `isReadyToStart`

`00` §2 replaced the `configured` state with a computed `isReadyToStart` "plus the reasons it is
not", and did not enumerate them. Two separate lists, because the client does two different things
with them — one disables the button, the other is a banner:

| `blockingReasons` | |
| ----------------- | --- |
| `no_qualified_attendees` | the drum is empty — only when a door list was actually read |
| `attendance_unavailable` | the seam is down right now and there is no copy |
| `attendance_too_many_rows` | over the 5,000 ceiling, §7 *(added by PGTI-116)* |
| `crm_unavailable` | |

**Unknown is not empty** *(PGTI-116)*. With no copy of the door list the drum is unknown, so
`no_qualified_attendees` is not reported beside `attendance_unavailable` — the rule
`IAttendanceSource` states for itself. **Over the ceiling, the raffle read stays `200`** with
`attendance_too_many_rows`; `GET …/attendees` answers `409 ATTENDANCE_TOO_MANY_ROWS`, and so does
`start`. **Outside `draft`, `isReadyToStart` and `blockingReasons` are absent** and no attendance is
read: only a draft has a start to be ready for. `warnings` stays, for `no_prizes` and, *(amended by
PGTI-118)*, for `awards_list_not_in_force`.

| `warnings` | |
| ---------- | --- |
| `awards_list_not_in_force` | §5.2, the step is being skipped. Outside `draft`, the step was skipped: read from `awards_batch_id IS NULL` *(PGTI-118)* |
| `attendance_copy_stale` | §8, serving a cached copy. `draft` only: after the freeze nothing is a copy |

**`start` takes no body and no acknowledgement.** Requiring the operator to tick off each warning
was considered: it would make a skipped business rule impossible to skip silently, which is
Decision 12's concern. It was rejected because the accountability already lives somewhere durable —
the acta states every warning in force at the freeze, signed by a notary and two witnesses — and a
checkbox produces no better record while adding a modal in front of a room, plus a race where a
warning appears between the read and the press and the raffle refuses to open.

The prize side adds one more warning, `no_prizes`, owned by `00` §2 (settled on PGTI-38). It never
blocks: prizes may be added while `in_progress`.

## 9. Endpoints, roles and error codes

| Route | Role | Notes |
| ----- | ---- | ----- |
| `POST /v1/awards-members` | `raffles:write` | §5.1. Replaces the list wholesale |
| `GET /v1/awards-members` | — | the batch metadata and a count. Never the addresses |
| `GET /v1/events/{id}/raffle/attendees` | — | §8. Both lists |
| `POST /v1/events/{id}/raffle/attendees/overrides` | `raffles:write` | body `{ email, decision, note? }` |
| `DELETE /v1/events/{id}/raffle/attendees/overrides` | `raffles:write` | body `{ email }` |

`raffles:write` covers editing the qualified list, exactly as `00` §11 says. There is no new role.

**The email goes in the body, never the path.** An address in a URL segment needs encoding, and it
lands in access logs — the same objection `convocatorias/06` §2.1 makes about putting a token in a
query string.

**`GET /v1/awards-members` returns a count and the batch metadata, not the addresses.** It exists so
an operator can see which list is in force and when it was loaded. Serving the membership itself
would turn a raffle setup screen into a bulk export of who is in a third party's loyalty programme.

**Overrides are refused once the raffle leaves `draft`.** `409 RAFFLE_NOT_DRAFT`. The list is
frozen; there is nothing to override.

### 9.1 How an override write is checked

*(Added by PGTI-117.)* An override is an edit to the raffle, so `00` §5's event gate applies. The
email is trimmed and lowercased before any comparison. Checks run in this order, and the first
failure answers:

1. Body: a valid `email`, `decision` of `included` or `excluded`, `note` at most 1,000 characters → `400`
2. Event exists → `404 EVENT_NOT_FOUND`
3. Raffle exists → `404 RAFFLE_NOT_FOUND`
4. Event `closed` or `cancelled` → `409 EVENT_NOT_EDITABLE`
5. Raffle not `draft` → `409 RAFFLE_NOT_DRAFT`
6. **Live** door list through `IAttendanceSource`, never the cache: unreachable →
   `503 ATTENDANCE_UNAVAILABLE`; over the ceiling → `409 ATTENDANCE_TOO_MANY_ROWS`; email not on it →
   `409 PARTICIPANT_NOT_CHECKED_IN`
7. **Include only:** walk-in → `PARTICIPANT_WALK_IN`; no contact → `PARTICIPANT_CONTACT_UNRESOLVED`;
   then CRM, live, for that one contact: unreachable → `503 CRM_UNAVAILABLE`; `suspended` →
   `PARTICIPANT_SUSPENDED`; `do_not_invite` → `PARTICIPANT_DO_NOT_INVITE`; absent →
   `PARTICIPANT_CONTACT_UNRESOLVED` (§3.1). *(Amended by PGTI-190: read from `canBeInvited`, §3.)*
8. Write. `204`

A write validates live and fails closed, per ADR-0013; the cache is for reads. The consequence is
accepted: until PGTI-130 replaces `UnavailableAttendanceSource`, every override write is `503`, as
`start` is.

**An exclude asks nothing of CRM and is refused for nothing but step 6.** It can never put anybody
into the drum, so there is no hole to guard. Excluding a walk-in or a blacklisted person is allowed
and changes nothing: their own reason ranks above `manually_excluded` (§6).

**`POST` upserts.** A second `POST` for the same email replaces the decision and the note and
re-stamps `decided_by_id` and `decided_at`, so flipping include to exclude is one call.

**`DELETE` is idempotent and does not consult the door.** `204` whether or not an override existed,
after steps 2–5. Skipping the door means an override for somebody whose check-in was reversed in
convocatorias can still be removed.

*(Added by PGTI-118.)* **Both writes are serialised on the raffle row.** Step 8 locks the raffle row
and checks step 5 again under the lock, then writes; `start` reads the overrides only after taking
the same lock. Without that, an override that passed step 5 could commit just after a `start`: the
operator is told it was saved, the override table says it was decided before the freeze, and the
frozen rows, which the acta prints, say otherwise. With it, the override either lands before the
freeze and is part of it, or answers `409 RAFFLE_NOT_DRAFT`.

| Code | Kind | When |
| ---- | ---- | ---- |
| `RAFFLE_NO_PARTICIPANTS` | Conflict | `start` with an empty drum, whatever emptied it *(PGTI-118)* |
| `ATTENDANCE_UNAVAILABLE` | Unavailable | `start` cannot read attendance live |
| `CRM_UNAVAILABLE` | Unavailable | `start` cannot reach CRM, including a broken circuit |
| `PARTICIPANT_WALK_IN` | Conflict | manual include naming a walk-in |
| `PARTICIPANT_SUSPENDED` | Conflict | manual include naming a person CRM has suspended (PGTI-190) |
| `PARTICIPANT_DO_NOT_INVITE` | Conflict | manual include naming a blacklisted person |
| `PARTICIPANT_CONTACT_UNRESOLVED` | Conflict | manual include naming somebody with no CRM contact, §3.2 |
| `PARTICIPANT_NOT_CHECKED_IN` | Conflict | override naming somebody not at the door |
| `ATTENDANCE_TOO_MANY_ROWS` | Conflict | over the 5,000 ceiling, §7 |
| `FILE_TOO_LARGE` / `FILE_TYPE_NOT_ALLOWED` / `UPLOAD_EXPANDED_TOO_LARGE` / `UPLOAD_NO_HEADER_ROW` | — | §5.1, same codes as `convocatorias/05` §7.1 |

A **broken circuit is a local exception, not a failure from CRM**. `BrokenCircuitException` maps
onto `CRM_UNAVAILABLE`, or the failure mode we most expect reaches the user as a `500`
(`convocatorias/05` §6.1).

`start` is serialised on the raffle row. Two operators pressing it at once means one `start` and one
`409 RAFFLE_NOT_DRAFT` (`00` §2), never two snapshots.

*(Amended by PGTI-118.)* The live reads of attendance and CRM run **before** the lock, so a slow
seam never holds it. Under the lock the state is checked again, the overrides are read, the pipeline
runs and the rows are written, in one transaction. `start` checks in this order, and the first
failure answers: event exists → raffle exists → event `closed` or `cancelled`
(`EVENT_NOT_EDITABLE`) → raffle not `draft` (`RAFFLE_NOT_DRAFT`) → live door list (`503
ATTENDANCE_UNAVAILABLE`, `409 ATTENDANCE_TOO_MANY_ROWS`) → CRM (`503 CRM_UNAVAILABLE`) → empty drum
(`409 RAFFLE_NO_PARTICIPANTS`). A refused `start` writes nothing.

## 10. English ↔ Spanish, for the frontend

Per Decision 26, a backend developer may do the wiring but **may not change any aspect of the UI**.
This table and the list below are the handoff.

| Backend | Mock's Spanish |
| ------- | -------------- |
| `attendee` | — (no equivalent; the mock has no door list) |
| `participant` | `participante` |
| `qualified` / `notQualified` | `calificados` / `noCalificados` |
| `exclusionReason` | `motivoExclusion` |
| `manualDecision: included` / `excluded` | `agregadoManual` / `excluidoManual` |
| `awardsMember` | `miembro DO Travel Awards` |
| `walkIn` | `asistente sin invitación` |
| `suspended` | `suspendido` |
| `doNotInvite` | `no invitar` |
| `dominicanDomain` | `dominio .do` |

**What the frontend has to absorb**, all of it Robert's card:

1. **The three participant modes are gone** (`evento` / `crm` / `externo`), along with
   `participantesExternos`, the six fabricated people at `NuevoSorteoModal.tsx:108`, and
   `filtroTipos[]`. Decisions 5 and 10, already ticketed as PGTI-41.
2. **There is no refresh button and there never will be.** `start` is the refresh. A button that
   appeared to refresh a live list would be doing nothing.
3. **Both lists arrive from one read**, with a reason per excluded person, and the UI moves people
   between them by writing an override — not by editing a list.
4. **A walk-in, a suspended person and a blacklisted person render as un-movable**, greyed with their reason. Offering
   a control that returns `409` every time is worse than showing none.
5. **`attendanceFetchedAt` and the warnings are contract, not decoration.** An
   operator freezing a drum needs to know the list is forty seconds old and that the awards step
   was skipped.

## Definition of done

- The six pipeline steps run in the order of §2, and a test fixes the order by proving a case where
  two orders give different drums: a blacklisted person with a manual include is **out**.
- A manual include of a `.do` address and of a non-member both put the person **in**, each carrying
  `overruled_reason`.
- `409 PARTICIPANT_WALK_IN`, `409 PARTICIPANT_CONTACT_UNRESOLVED`, `409 PARTICIPANT_SUSPENDED` and
  `409 PARTICIPANT_DO_NOT_INVITE` on manual include, and **nothing is written** — assert the override table is unchanged, not only the status code.
- An override written before a person is blacklisted survives in the table, loses at compute, and
  the frozen row records that it was overruled.
- **An attendee whose CRM contact is `opted_out` is in the drum.** This is the regression test:
  "suppressed" reads like "exclude" to anyone skimming.
- **`canBeEmailed` alone changes nothing.** A contact that is `canBeEmailed: false` with
  `suppressionReason: "opted_out"` qualifies. Pin it, or the branch widens back to all three reasons.
- *(PGTI-190.)* A blacklisted contact reported as `opted_out` is **out**, `do_not_invite`; a
  suspended contact is **out**, `suspended`; and an include on either is refused. A row without
  `canBeInvited` fails the check as `crm_unavailable`.
- An id absent from the mailability response is treated as not cleared, not as cleared: reason
  `contact_unresolved` (amended by PGTI-117).
- `checked_in = qualified + not_qualified` holds on every frozen snapshot, and the exclusion reasons
  are disjoint — assert the sum, not the individual buckets.
- No awards batch in force: the step is skipped, `start` succeeds, `awards_batch_id` is null, and
  the warning is present. A batch in force matching nobody: `409 RAFFLE_NO_PARTICIPANTS`.
- A second awards upload replaces the first wholesale — assert the previous addresses are **gone**,
  which is what distinguishes replace from append.
- Uploading a file where every row is an invalid address succeeds with `accepted: 0`.
- A draft read with the attendance seam down returns the previous copy with `attendanceStatus:
  stale`; `POST start` with the same seam down is `503 ATTENDANCE_UNAVAILABLE` and **writes no
  rows**. Assert the absence of rows, because a partial freeze is the failure that cannot be undone.
- `start` never reads through the cache — assert the seam is called during `start` even when a fresh
  copy exists.
- Overrides after `start` are `409 RAFFLE_NOT_DRAFT`.
- No `raffle_participant` table exists anywhere in the migration set — the table is
  `raffle_attendee`. Assert the absence, since the old name appears in ADR-0022's prose.
