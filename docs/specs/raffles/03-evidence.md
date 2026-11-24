# The Raffle Evidence Trail — Spec

What the raffle leaves behind: the redemption code a winner is given, the handover that proves a
prize left MITUR's hands, the notarial record that attests the draw, and the email that tells a
winner they won. This is the half with legal weight.

Three artefacts, three different facts, and none of them stands in for another:

| Artefact | Attests | Fixed when |
| -------- | ------- | ---------- |
| **NotarialRecord** (*acta*) | what happened in the room: who was in the drum, every draw, every void | `complete` |
| **Handover** (*entrega*) | that one prize physically reached one named, identified person | the handover |
| **RedemptionCode** (*código de canje*) | nothing. It finds a draw | the draw |

Settled elsewhere and not re-argued here: ADR-0022 (the record is generated once by the backend and
stored through the file seam, the handover signature goes through the same seam, winners are
emailed via notification-service, WhatsApp is dropped), ADR-0014 (the file seam, its sniffing and
its read posture), ADR-0020 (a failed notification never fails a business transaction), ADR-0024
(the PDF library), `00` (the tables, the state machine, the authorities required at `complete`),
`01` (the frozen `raffle_attendee` table and its warnings), `02` (draws, voids, the pool).

**Amended 2026-10-05 (PGTI-121 grilling).** The lookup's normalisation also reads the Crockford
aliases (`O` as `0`, `I` and `L` as `1`) and accepts the eight characters without `RD`, and anything
that does not normalise to a well-formed code is `404 REDEMPTION_CODE_NOT_FOUND`, never `400` (§2.4).
A draw's `status` (`drawn`, `redeemed`, `voided`) is not the prize axis. It describes one draw, and
every draw in a prize's history carries it, in `GET …/raffle` and the handover response alike
(§1, §3.4). The handover response's prize shape is the one `GET …/raffle` returns, so both expose
each draw's `redemptionCode` and its handovers, receiver's document and signature id included, under
§7's posture. The signature's 1 MB is a constant, not configuration (§3.2). A handover checks the
prize's state once, under the lock, after the bytes are saved, so a refused one discards its bytes;
an unlocked check beforehand was considered and rejected (§3.4). A multipart `receiverIsWinner`
accepts only `true` or `false`, in any case (§3.1).

**Amended 2026-10-06 (PGTI-122 grilling), and again 2026-10-27 (PGTI-210 grilling).** The record's
times print in the local time of the event's city, never UTC: a notary checks them against their
watch (§4.2 item 3). The time zone belongs to the RoadshowCity, an IANA id, not to the raffle, so
the place the record states and the clock it writes in cannot disagree. The place is the event's city, "Ciudad, País", because an Event has exactly one; data that still
carries more than one live city prints all of them in display order, never refusing and never
dropping one (§4.2 item 2). Each `notarialRecords` entry names its author as `generatedById`, the
same shape as `startedById` (§4.6). `complete` answers `200` with the raffle (`00` §2).

## Decisions behind this shape

Settled with the user on 2026-09-25 in a grilling session on PGTI-37. Four of them change what `00`
wrote down rather than only filling gaps:

- **Six tables, not four.** `00` §1 said "four tables, and no fifth". Handovers get their own table
  so a reversed one keeps its row (§3.3), and the record gets a versions table so a reissue keeps the
  old one (§4.5). `raffle.acta_stored_file_id` moves into the latter and is not created.
- **A `completed` raffle accepts two more writes**, `reissue` and handover reversal, beside the
  handover `00` §4 already allowed.
- **`complete` also requires `pickupInstructions`** (§5.3), beside the six authority fields of `00` §6.
- **`00` §4 and `02` §7 disagreed** about the record's layout, grouped by round against
  chronological. It carries both, as two sections (§4.2). Neither spec needs editing.

One departs from the rule it sits inside, deliberately:

- **The winner email reads `Event.Language`**, which `events-service/CLAUDE.md` forbids for the
  quote alerts. Those go to MITUR staff, and these go to attendees (§5.4).

Everything else answers the five questions the ticket left open: the code's generation and
uniqueness, the signature's format, the PDF library, what the record states, and the email contract.

## 1. The tables this spec adds

```
raffle ──< raffle_round ──< prize ──< prize_draw ──< prize_handover
   └──< notarial_record
```

| Table | Grain | Holds |
| ----- | ----- | ----- |
| `prize_handover` | **one per handover attempt** | `prize_draw_id`, the receiver (§3.1), `signature_stored_file_id`, `redeemed_at/by_id`, the reversal columns |
| `notarial_record` | **one per version** | `raffle_id`, `version`, `stored_file_id`, `generated_at/by_id` |

Plus, on existing tables:

| Table | Adds |
| ----- | ---- |
| `raffle` | `pickup_instructions` — free text, at most 1,000 characters, required at `complete` |
| `prize_draw` | `redemption_code`, `winner_notification_id`, `winner_notification_status` |

`00` §1's "the redemption columns" on `prize_draw` are **not** created. They live on
`prize_handover`.

Constraints that carry the grain:

- `prize_draw.redemption_code` is **unique across the table**, not per raffle (§2.3).
- `prize_handover` has a **partial unique index on `prize_draw_id WHERE reversed_at IS NULL`**: at
  most one live handover per draw. Same shape as `00`'s one live draw per prize.
- `notarial_record` is **unique on `(raffle_id, version)`**.
- `winner_notification_status` is checked to `sent` / `no_recipient` / `failed` or null, copying
  `ck_quote_decision_outcome_notification_status`.
- `prize_handover.signature_stored_file_id` and `notarial_record.stored_file_id` are foreign keys to
  `stored_file` and **both are registered in `StoredFileReferences`**. The registry test fails
  without them, which is the point.

The prize axis of `00` §3 is re-derived against the new table. It is still derived, never stored:

| Derived | When |
| ------- | ---- |
| `undrawn` | no live draw |
| `drawn` | a live draw with no live handover |
| `redeemed` | a live draw with a live handover |

## 2. The redemption code

### 2.1 What it is for

**A claim reference, not a credential.** It finds the right draw quickly, typed at a desk or
scanned later (PGTI-40). It proves nothing. Whoever presents it still identifies themselves at the
handover, and the handover is the evidence (§3). So a forwarded email collects nothing on its own.

The mock treats it as neither: its scanner redeems on a substring of the code **or the winner's
name**, with no confirmation (`PwaScannerPage.tsx:323-328`). A pure bearer code was also rejected.
Prizes are collected at a desk days later, often by a colleague, and a bearer code turns a
forwarded email into a stolen prize with nothing on record to contest it.

### 2.2 Generation

- **Inside the `draw` transaction** (`02` §2, step 5), on the `prize_draw` row it identifies.
- **`RandomNumberGenerator`**, the rule `00` §10 sets for the draw. No `Math.random`, no clock.
- **Format `RD-XXXX-XXXX`**: eight characters from the Crockford base-32 alphabet
  (`0123456789ABCDEFGHJKMNPQRSTVWXYZ`, no `I L O U`). That is 32⁸ ≈ 1.1 × 10¹². Nothing misreads
  off a phone screen, and guessing a neighbour is hopeless even though the code is not the security.

The mock's `RD-XXXX-NNNN` (`mockSorteo.ts:536`) ends in the last four digits of `Date.now()`, which
is exactly the guessable half. It goes.

**A voided draw keeps its code, and the code is dead.** A redraw is a new draw with a new code
(`02` §3). Nothing is regenerated in place, so a code printed in a winner's email always means the
one draw it was issued for.

### 2.3 Uniqueness is global

One unique index over `prize_draw.redemption_code`, across every raffle. The desk and the scanner
look a code up **without knowing which raffle it came from** (§2.4). The mock's scanner already
searches every raffle (`PwaScannerPage.tsx:277-337`). A per-raffle key would make a scan ambiguous
across two events.

The draw checks that a generated code is unused before inserting it, and generates another on a hit,
up to three attempts; a third collision fails the draw as `DB_CONFLICT`.
The index is the backstop for two draws racing to the same code. A violation there aborts the
transaction, so it fails the draw as `DB_CONFLICT`, writes no draw, and the operator presses again.
At 10¹² neither path is a real event. Both exist so the uniqueness is stated rather than assumed.

### 2.4 The lookup

```
GET /v1/prize-draws/by-code/{code}        authenticated
```

Flat, not under the event, because the caller does not know the event. That is the reason the
route exists. Input is normalised before matching: case-insensitive, hyphens and spaces ignored, so
`rd7k3mq9xa` finds `RD-7K3M-Q9XA`. *(Amended by PGTI-121:)* the Crockford aliases are read as the
digits they look like, `O` as `0` and `I` or `L` as `1`, and the `RD` prefix is optional, so
`7K3MQ9XA` finds it too. The normalised input is matched exactly against the unique index. Input
that does not normalise to eight characters of the alphabet is answered like any unknown code.

```json
{ "drawId": "…", "eventId": "…", "raffleId": "…", "prizeId": "…",
  "prizeName": "Botella de ron", "winnerName": "María Pérez", "winnerCompany": "Hotel Sol",
  "status": "drawn",
  "void": null,
  "handover": null }
```

`status` is `drawn`, `redeemed` or **`voided`**, and a dead code is answered, never hidden:

| The code's draw | Answer |
| ---------------- | ------ |
| live, no handover | `200`, `drawn` |
| live, handed over | `200`, `redeemed`, with `handover: { receiverName, redeemedAt }` |
| voided | `200`, `voided`, with `void: { code, voidedAt }` |
| no such code, or malformed input | `404 REDEMPTION_CODE_NOT_FOUND` |

The desk needs to tell a winner "that draw was declared void", not "invalid code". A bare `404` for a
code that once existed makes the operator doubt the code, the scanner and the winner, in that order.

The scanner terminal calls this route only under `raffles:draw`, and links a `drawn` code to the
handover rather than recording one (`convocatorias/10-scanner-terminal.md` §3).

**The lookup never redeems.** Recording a handover is always `POST …/handover` (§3) with a receiver,
a document and a signature. The mock's scan-and-auto-redeem is what §2.1 and §3 exist to refuse.

**There is no search by name.** Finding a winner by name is the raffle screen's job, from
`GET …/raffle`. A code lookup that also matches names is how the mock redeems the wrong María.

Reads carry no policy, per `00` §10.

## 3. The handover

### 3.1 What it records

```
POST /v1/events/{eventId}/raffle/prizes/{prizeId}/handover        raffles:draw
multipart/form-data
```

| Part | |
| ---- | - |
| `receiverName` | required, at most 200 characters. Who physically took it |
| `receiverIdentityDocument` | required, at most 100 characters, free text and opaque. The treatment `00` §6 gives the authorities |
| `receiverIsWinner` | required boolean. **Stated by the operator** |
| `signature` | required file. §3.2 |

Written to one `prize_handover` row against the prize's live draw, plus `redeemed_at` and
`redeemed_by_id`.

**Collecting on the winner's behalf is allowed, and stated.** A colleague collecting for someone who
left early will happen. Refusing it pushes the handover off the system, the argument `00` §4 made
for surprise prizes.

**`receiverIsWinner` is stated, never derived.** Comparing `receiverName` against `winner_name` lies
in both directions: "María Pérez" against "Maria Perez", or two different people who share a
name.

**The identity document is required even when the receiver is the winner.** That is the check the
mock's "Certifico documento de identidad" checkbox pretended to do (`EscanerCanjeModal.tsx:147-155`)
and never stored. Recording what was seen is the only form of that check that leaves evidence.

No photograph of the receiver. Considered and declined.

### 3.2 The signature

**Captured on a signature pad in the browser, and sent as PNG.** `canvas.toBlob('image/png')` is
universal and lossless, and a line drawing is a few tens of kilobytes.

- **PNG only**, by magic bytes, through the existing `UploadPolicy`. A JPEG or a PDF of a signature
  means someone photographed paper, which is a different act, and accepting it makes the pad
  optional in practice. If the tablet dies, the answer is to record the handover later, not to
  photograph paper.
- **At most 1 MB.** Far above any real canvas, and far below the general 25 MB. It is a rule for
  this owner in `UploadPolicy`, the way event images already refuse PDFs (`InspectImageAsync`), not
  a change to the shared limit and not a new file kind in the seam.
- **No server-side blank check.** It would need image decoding the platform does not have, and a
  scribble defeats it. The frontend keeps "confirm" disabled until the pad has ink, and the operator
  standing at the desk is the real control.

Refusals reuse the seam's codes: `FILE_TOO_LARGE`, `FILE_TYPE_NOT_ALLOWED`, `FILE_TYPE_MISMATCH`.

**One call carries the fields and the signature**, per ADR-0014's "one multipart call per owner". A
handover and its signature land together or not at all. Bytes land before the row, and a failed
insert is compensated by the chassis's `DiscardOrphanBytesAsync`, as for every other upload. The
store being unreachable is `503 FILE_STORE_UNAVAILABLE`, and nothing is written.

The mock has no signature at all. `firmaDigital` is the fixed string `'Firma Digital Validada en
Terminal MITUR'` (`EscanerCanjeModal.tsx:43`). This is a new capability, not a port.

### 3.3 Reversal

```
POST /v1/events/{eventId}/raffle/prizes/{prizeId}/handover/reverse        raffles:draw
{ "reason": "Se marcó el premio equivocado; María retiró la botella de ron." }
```

The case: the operator picked the wrong row. María collected the rum, and the tote bag's draw was
marked redeemed. Without reversal the rum reads "not collected" for ever and the tote bag can never
reach its real winner.

`reason` is required, at most 500 characters. The live handover is **kept**, stamped `reversed_at`,
`reversed_by_id` and `reversal_reason`, and the prize drops back to `drawn`. Its signature file stays
referenced. A second handover is an ordinary `POST …/handover`. It is the same idiom as the void:
nothing is wiped, and the history is the rows.

The mock's one-click un-tick (`marcarPremioEntregado`, `mockSorteo.ts:590-604`) cannot exist here.
It destroys the record of a signature somebody gave.

Editing a handover in place was rejected: it loses the signature and the values it replaced.

### 3.4 When

| | `undrawn` | `drawn` | `redeemed` |
| --- | --- | --- | --- |
| `handover` | `409 PRIZE_NOT_DRAWN` | yes | `409 PRIZE_ALREADY_REDEEMED` |
| `handover/reverse` | `409 PRIZE_NOT_REDEEMED` | `409 PRIZE_NOT_REDEEMED` | yes |

Both are permitted while the raffle is `in_progress` **or `completed`**, and neither consults the
event's status (`00` §5). Winners collect at a desk days later. In `draft` no prize can be drawn, so
the table above already answers.

Both **lock the `raffle` row**, the `02` §4 idiom (ADR-0028), so a handover racing a void waits and
answers `PRIZE_NOT_DRAWN`, or the void waits and answers `PRIZE_ALREADY_REDEEMED`. Without the lock, a
void and a handover could both commit, leaving a signature on a voided draw. Two desks redeeming the same draw is also caught by §1's
partial unique index.

**A redeemed prize still cannot be voided** (`02` §7). Reversal is the only way back from
`redeemed`, and it records why.

The response is `200` with the whole prize, as in `02` §2, now carrying each draw's `redemptionCode`
and its handovers, live and reversed, newest first. *(Amended by PGTI-121:)* it is the prize shape
`GET …/raffle` returns, and each draw in it also carries its `status`, `drawn`, `redeemed` or
`voided`, the same value the lookup answers.

## 4. The notarial record

### 4.1 What it is, and what it is not

**The record covers the draw only.** It attests what the notary and two witnesses saw: who was in
the drum, every draw including the voided ones, the prizes left undrawn. **It says nothing about
handovers.** The notary did not see next Tuesday's desk, and each handover carries its own signed
evidence (§3). This is also why the two axes of `00` §3 never touch: generating the record closes
nothing about redemption.

**What the platform stores is the text given to the notary to sign.** No notary signs a PDF the
platform generated. That would take a Dominican notary's certificate-signing integration, which does
not exist. The document is printed and signed in ink, and **the ink-signed original stays on paper**,
outside the platform.

Uploading a scan of the signed original is the **planned addition**, deliberately not built now,
because the client has not yet settled how the paper will be handled. It is purely additive: one
link, one owner-scoped upload, one more write permitted on a `completed` raffle. Until it exists,
the frontend must not present the stored PDF as the signed original ("Acta generada", not "Acta
firmada").

### 4.2 What it states

In this order. The Spanish is the document's. The English here is the spec's.

**Header and preamble**

1. Title, the record's number (the raffle's id, shortened for display) and **its version** (§4.5).
2. **The event**: name, dates, city (*amended by PGTI-122:* the event's city, "Ciudad, País"; an
   Event has one). These are events' own data, read at generation time and frozen into the text.
3. **The times, from the data**: `started_at` (the freeze) and `completed_at`. **Never the moment of
   generation presented as the draw's.** The mock prints "siendo las {now} horas"
   (`ActaNotarialModal.tsx:91`), which states the wrong time. *(Amended by PGTI-122, then by
   PGTI-210:)* both print in the local time of the event's first live city by display order, its
   `roadshow_city.time_zone`. Every time the record prints, these two, each manual decision's and
   every line of the draw log included, reads `14:32 (hora de Chicago, UTC−05:00)`: the city as
   stored, and the offset from UTC of that instant, so an evening that crosses a daylight-saving
   change shows two offsets and two draws twenty minutes apart cannot read as out of order. The
   minus is U+2212; a zero offset is `UTC+00:00`. The body closes, before the authorities, with
   *Las horas están en la hora local de {Ciudad}, {País}; cada una indica su diferencia con UTC.*,
   the country as stored. Data that still carries more than one live city takes the first one's zone
   and names it; it never refuses. A city's zone can be corrected after completion: the moments
   never change, only the clock they are written in, and the next reissue (§4.5) prints the
   corrected times. The correction goes through the city PATCH, so it is possible on an event of
   any type that is not closed or cancelled (PGTI-335). A closed or cancelled event stays out of
   reach on purpose, since a wrong zone shows when the record is read on the night, long before the
   event closes. Cities created before the column read `America/Santo_Domingo`, what every record
   issued until then printed.
4. **The rules in force**: one prize per person, one prize per company, each on or off. *(Amended by
   PGTI-275:)* they can change while the raffle runs (`02` §1.2), and each draw stores the pair it
   ran under. The record compares the draws with each other, never with the raffle's current values:
   - **Every draw ran under the same rules**: the two fields print once, as that pair, even when the
     rules changed after the last draw. With no draws they are the raffle's stored values.
   - **The draws differ**: this item says *"Las reglas cambiaron durante el sorteo; cada sorteo
     indica las suyas."* instead of the two fields, and every draw in the draw log (item 10), voided
     ones included, ends with *"Un premio por persona: sí|no. Un premio por empresa: sí|no."*
5. **The warnings in force at the freeze**, as `01` §5.2 committed: the awards list not applied
   (`awards_batch_id` null). Plus which awards list was in force, by upload date. *(Amended by
   PGTI-114: attendance always comes from the door scan, so there is no uploaded-attendance warning
   and no dormant blacklist to report, `01` §7.1.)*
6. **How winners were chosen, stated truthfully**: a uniform draw from the eligible pool using the
   operating system's cryptographic random number generator, with the pool recomputed for each
   prize. The mock's "algoritmo computacional criptográfico **determinista**"
   (`ActaNotarialModal.tsx:96`) is false and goes.

**The arithmetic**

7. Checked in, in the drum, excluded, with the excluded broken down by `exclusion_reason` in `01`
   §6's order. `checked_in = qualified + not_qualified` holds on the page because it holds on the
   frozen table.

**Manual decisions**

8. **Each one, by name.** Person (name, company), direction (included or excluded), the rule it
   overruled (`overruled_reason`, e.g. *dominio .do*), who decided and when. **The override's
   `note` is not printed.** It was written as a note to the next operator, not as a statement for a
   notarised document, and printing it would change what operators write there. It stays in the
   system.

   ADR-0022's defence of hard-coding the `.do` rule was that the escape hatch "is recorded and
   visible on the acta". A count would not have honoured that. A named line does.

**Results, by round**

9. Rounds in `number` order, each a heading, which is `00` §4's "a heading somebody reads aloud".
   Under each, its prizes in creation order: name, sponsor, and the outcome. The outcome is the live
   winner's name and company, or *no adjudicado*. A prize created after `started_at` is marked
   *añadido durante el sorteo* (`00` §4). The sponsor is the company's name only, never the
   contact, read from the cached company directory when the acta is generated; a sponsored prize
   whose company name cannot be found at all, cold cache and CRM down, refuses the generation as
   `503 CRM_UNAVAILABLE` and writes nothing, and the operator retries.

**Draw log, chronological**

10. Every `prize_draw` row ordered by `drawn_at`, live and voided, which is `02` §7's "the order
    they happened". Each gives the time, round, prize, winner (name, company) and
    **`eligible_pool_size`**. For a void, it gives the time, the reason code in Spanish and **the
    void's note**.

    Unlike the override note, the void note prints. It is part of the void's stated reason, `02`
    §3 requires it for `other`, and it was written at the moment of voiding, in front of the room.

    The pool size is here and not in the results because it is the number an auditor checks against
    the annex, and it only means something per attempt.

**Authorities**

11. Notary and two witnesses, each name and `identity_document`, each with a line to sign.

**Annex: the participants**

12. Every **qualified** attendee, numbered `1..n`, sorted by last name then first name (accent- and
    case-insensitive), with name and company. **No email.** It is a printed, handed-around document
    kept for years, which is `02` §5's reason for keeping email off the projector. The manually
    excluded are not here, since they were not in the drum. They appear only in item 8.

    A notary attesting "380 people were in the drum" with no list is attesting a number they cannot
    check. The annex is what makes the count defensible, and what "was I even in it?" is answered
    from.

**Footer**, on every page: record number, version, *página X de Y*.

### 4.3 What it does not state

| Absent | Why |
| ------ | --- |
| Redemption codes | A claim reference for the winner alone (§2.1). They serve no purpose on a filed document |
| Handovers | §4.1 |
| Email addresses | §4.2 item 12 |
| The override note | §4.2 item 8 |
| Any "sello criptográfico" or validator URL | The mock's seal is `Date.now()` in base 36 and its URL is invented (`ActaNotarialModal.tsx:17-18`). The stored file already carries a checksum (ADR-0014), and a hash printed inside a PDF cannot cover that PDF |
| Placeholder authority names | Banned by `00` §6 |
| A logo | Not for now. It would be a bundled image asset, and it is the client's call |

### 4.4 Generation, at `complete`

`POST …/raffle/complete`, in one transaction:

1. **Lock the raffle row** (`02` §4) and load it. Refuse unless `in_progress`.
2. Require the six authority fields (`RAFFLE_AUTHORITIES_REQUIRED`, `00` §6) **and
   `pickup_instructions`** (`RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED`, §5.3). Both are
   `ErrorKind.Conflict`, and both are checked before anything is rendered.
3. Fix `completed_at` once, and use that one value for both the row and the document.
4. Resolve what the document names but events-service does not store as text: **operator display
   names** for the manual decisions (item 8), read from the realm by user id. An id the realm cannot
   resolve prints as *usuario no disponible* with the id, and **never fails `complete`**. Unlike
   ADR-0013's read-time resolution this is safe, because the document freezes the text.
5. Render the PDF in memory, behind a Core seam. The library is Infrastructure's (ADR-0024).
6. Save the bytes through `IFileStore`, then write `stored_file`, `notarial_record` version 1 and
   `state = completed`.
7. Commit.
8. **After the commit**, send the winner emails (§5) — in the background since A31 (2026-11-20):
   `RaffleWinnerNotificationWorker` sends them after the response, which carries no email outcome.

**`complete` fails closed.** If the store is unreachable it is `503 FILE_STORE_UNAVAILABLE`, the
raffle stays `in_progress`, and the operator presses again. Nothing is lost while it waits:
`in_progress` permits `complete` indefinitely. That keeps one invariant, **a `completed` raffle
always has a record**, so no screen ever has to render a completed raffle with no document, and
winners are emailed only once the evening is truly closed.

This does not contradict `00` §5's "a raffle that legitimately started must always be able to
finish". That rule is about the event's status stranding a raffle *permanently*. A storage outage is
temporary, and a retry answers it.

Completing anyway and generating later through `reissue` was rejected. It creates a
completed-without-a-document state that every reader of the raffle has to handle.

### 4.5 Reissue

The case: `complete` is pressed, the PDF printed, and the notary reads it before signing. One
witness's cédula has two digits transposed. The draw data cannot be wrong, because it is frozen. The
six authority fields can be, because somebody typed them.

```
POST /v1/events/{eventId}/raffle/reissue        raffles:draw
{ "notaryName": "…", "notaryIdentityDocument": "…",
  "witnessOneName": "…", "witnessOneIdentityDocument": "…",
  "witnessTwoName": "…", "witnessTwoIdentityDocument": "…" }
```

- **Only while `completed`**, otherwise `409 RAFFLE_NOT_COMPLETED`. Stamps the raffle row.
- **The body is the six authority fields, all required**, and they are the only thing it changes.
  `PATCH` on a `completed` raffle stays refused (`00` §4). Reissue is one atomic call, not "patch
  then regenerate", so there is no window in which the stored fields and the latest document
  disagree.
- Writes version *n + 1*: new bytes, new `stored_file`, new `notarial_record` row. **Earlier
  versions are never deleted or unlinked.** An auditor can see every version and when it replaced
  the last. *(Amended by PGTI-123:)* every reissue writes *n + 1*, even when the six fields are
  unchanged. There is no "unchanged" error and no idempotent short-circuit: an extra version harms
  nothing, and a re-render may legitimately pick up a renamed event or operator.
- Version *n > 1* states on its first page: *Versión n. Reemplaza la versión n − 1, generada el …*.
  *(Amended by PGTI-123:)* the line names only *n − 1* and its `generated_at`, as
  `dd/MM/yyyy HH:mm`, followed by the same zone label as every other time (§4.2 item 3,
  *amended by PGTI-210*), directly under the title block and before the event. Version 1 has no
  such line. The record states no generation time of its own.
- **Rendering is the same function as at `complete`.** Everything is re-read and re-resolved. The
  frozen data cannot move, but events' own event particulars and the realm's display names can, so
  a renamed event or operator shows as a difference between versions. That is accepted: neither is
  what the record attests.
- **Never re-sends a winner email.** The email keys on the draw (§5.2), not on the record.
- Fails closed on the store, like `complete`, and leaves the previous version current.
- *(Amended by PGTI-123:)* **Ignores the event's status**, like `complete` (`00` §5). A notary may
  spot the error days after the event closed. Only the event and raffle `404`s apply.

`raffles:draw`, like `complete`: the role that produces the document corrects it.

### 4.6 Reading it

`GET …/raffle` carries `notarialRecords`, newest first:

```json
"notarialRecords": [
  { "version": 2, "storedFileId": "…", "generatedAt": "…", "generatedById": "…" },
  { "version": 1, "storedFileId": "…", "generatedAt": "…", "generatedById": "…" } ]
```

*(Amended by PGTI-122:)* `generatedById`, not `generatedBy`, the same shape as `startedById`.

The bytes come from the existing flat `GET /v1/files/{id}/content`. No raffle-specific download
route. The file keeps ADR-0014's degradation: `FILE_MISSING` is not `FILE_STORE_UNAVAILABLE`.

## 5. The winner email

### 5.1 When

**Once, at `complete`, after the commit: one email per live draw.**

- **After each draw** was rejected. The commonest void, `absent`, happens seconds after the draw,
  and "¡Felicidades!" to somebody voided a minute later is an email that cannot be unsent. That is
  the test the quote alerts apply.
- **A manual "notificar" button per prize**, the mock's (`NotificarGanadorModal.tsx`), was rejected.
  It is one more button in front of a room, and it gets forgotten for some prizes.

At `complete` no void is possible any more (`02` §7), so every email goes to a final winner. A
winner who left early hears at the close of the evening, not within the minute, and pickup is days
later anyway.

It follows ADR-0020 and the quote alerts in full. Post-commit and swallowed, so **no email outcome
ever fails `complete`**. Never throws, cancellation included. The bookkeeping write takes
`CancellationToken.None`. See `events-service/CLAUDE.md` §"A quote's alerts…", whose rules apply
here unchanged, including the accepted loss that a crash between commit and send loses the email
silently.

### 5.2 The contract

`SendNotificationRequest` (`Infrastructure/Notifications/NotificationContracts.cs`), one request per
draw. Each winner's copy differs only in `data`, but a batch per draw keeps the idempotency key
and the recorded outcome on one row each.

| Field | Value |
| ----- | ----- |
| `type` | `events.prize.won` |
| `language` | §5.4 |
| `subject`, `body` | the seeded template, placeholders intact. events-service renders nothing |
| `recipients` | one: `winner_email`, with `name` = `winner_name`, both **from the draw's snapshot** |
| `about` | two entries: the event and the raffle. **Never the draw**: nobody asks for mail about one attempt |
| `idempotencyKey` | `events:raffle:{raffleId}:draw:{drawId}:won` |

**The recipient is the frozen snapshot, not a fresh CRM lookup.** The email goes to the address the
person was entered under. A lookup would reopen ADR-0022's exception from the other side.

The key is per draw, never random. A retry of the same `complete` replays, and `reissue` has no path
to it.

The outcome lands on the draw: `winner_notification_id` (the batch id from the `202`) and
`winner_notification_status`, `sent` / `failed` / `no_recipient`. It is the same vocabulary as
`QuoteNotificationStatus`, answering "was María told?" from the raffle screen, which the mock only
claimed (`NotificarGanadorModal.tsx:149`). `no_recipient` is defensive: `01` §6 makes a person
without an email unqualifiable, so a live draw always has one.

### 5.3 What it says

The winner's name, the prize's name, the event's name and date, the **redemption code**, and the
raffle's **`pickupInstructions`**.

- **The code goes in.** The winner is its one legitimate holder, and without it the email is not
  actionable. A forwarded email still collects nothing without the receiver's document and signature
  (§2.1, §3).
- **No value, no pool size, nobody else's name.**
- **No sponsor.** The sponsor fields are optional (`00` §1), and the template dialect has **no
  conditionals**, so an absent sponsor would print as a dangling sentence. That is the withdraw
  template's rule in `events-service/CLAUDE.md`. Adding it back requires the sponsor to be
  required, which is a separate decision.
- **`pickupInstructions` is free text on the raffle**: where and until when, e.g. *Stand de MITUR,
  pabellón 4, hasta el viernes 18:00*. It is in `00` §9's `PATCH` body, so omitting it clears it,
  per ADR-0009. It is **required at `complete`** as `409 RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED`, for
  `00` §6's reason: the guard sits where the need is. The mock's "stand oficial de acreditaciones"
  is wrong for any event abroad, and a winner's email with nowhere to go is the one the client will
  hear complaints about. Instructions per prize were not built, since nobody has described sponsors
  running their own desks.

The copy is seeded by `InsertData` in its own migration with its own frozen seed class, and pinned by
a golden-file test. That is exactly the quote alerts' discipline, and editing the copy is a new
migration.

### 5.4 Language

**`Event.Language`, with templates seeded for `es` and `en` only, falling back to `es`.**

The quote alerts hard-code `es` because their readers are MITUR staff. Raffle winners are
attendees, often foreign tour operators at an event abroad, and `Event.Language` is exactly what
convocatorias picks the invitation template from. A German agent invited in German does not win in
Spanish.

The fallback **cannot be a refusal**. Convocatorias refuses to launch a campaign whose language has
no template, which is safe before anything is sent. Here `complete` must never fail over an email,
and sending nothing is worse than sending Spanish. notification-service already records each
batch's `language`, so the fallback is visible in the history.

A French template later is one seed migration and no code. `pickupInstructions` is in whatever
language the operator typed it. The model does not police that.

Dates in `data` are formatted by C# in the template's language, since the dialect formats nothing.

## 6. The certificate is the frontend's keepsake

`CertificadoPremioModal.tsx` builds a per-winner "CERTIFICADO OFICIAL DE PREMIO" PDF in the browser.
**It is not evidence, the backend generates nothing for it, and this spec guarantees nothing about
it.** It attests nothing the record, the email and the handover do not. It is the thing a sponsor
hands over in a photograph.

If it survives, it is decorative, built from `GET …/raffle`, and carries **no seal, no validator
URL and no redemption code**. The code is left off because a nice PDF gets posted on social media,
and the code belongs to the winner's email. Whether it survives, and how it looks, is the frontend
owner's call (Decision 26).

## 7. Who can read what

Unchanged from the platform, deliberately. Raffle reads carry no policy (`00` §10), and every file
inherits its owner's read posture (ADR-0014). So **any authenticated user can read a receiver's
identity document, their signature and the record's participant annex.**

Today "authenticated" means MITUR staff, and that is tolerable. A read policy just for this feature
would be the first brick of the read-authorisation decision ADR-0014 deliberately left open, and the
flat `GET /v1/files/{id}/content` would have to learn which owners are restricted.

> **Blocker on C2 and PGTI-40.** Decision 22 plans a narrow `event_staff` composite for door staff.
> They will be authenticated too, so under the fallback policy a temp at the door could read every
> winner's document number and signature. **Whoever creates that composite settles read
> authorisation first.** It must not ship on the fallback policy.
>
> **Settled by ADR-0025 (PGTI-39, 2026-09-26).** The fallback now requires `platform:staff` or
> `platform:service`, and `event_staff` holds neither, so these reads stay shut to door staff with
> no raffle route changed. `event_staff` is created only after that is live
> (`convocatorias/09-check-in.md` §8.2).

Storing the document's type instead of its number was considered and rejected: the number is the
evidence.

## 8. Endpoints and error codes

Additions to `00` §10 and `02` §8. Under `v{version:apiVersion}/events/{eventId:guid}/raffle` unless
flat:

| Route | Role | Notes |
| ----- | ---- | ----- |
| `POST "complete"` | `raffles:draw` | `00`'s. Now also requires `pickupInstructions`, generates version 1, fails closed. §4.4 |
| `POST "reissue"` | `raffles:draw` | §4.5. `completed` only. Body: the six authority fields |
| `POST "prizes/{prizeId:guid}/handover"` | `raffles:draw` | `00`'s route, now defined. Multipart. §3 |
| `POST "prizes/{prizeId:guid}/handover/reverse"` | `raffles:draw` | §3.3. `{ reason }` |
| `GET v{version}/prize-draws/by-code/{code}` | — | **flat**. §2.4 |

| Code | Kind | When |
| ---- | ---- | ---- |
| `RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED` | Conflict | `complete` without `pickupInstructions` |
| `RAFFLE_NOT_COMPLETED` | Conflict | `reissue` on a raffle not `completed` |
| `PRIZE_NOT_REDEEMED` | Conflict | `reverse` with no live handover |
| `REDEMPTION_CODE_NOT_FOUND` | NotFound | lookup of a code that never existed |
| validation | Validation | a handover field missing or over length, a missing signature part, a missing or over-long `reason`, an authority field missing on `reissue` |

Reused unchanged: `RAFFLE_AUTHORITIES_REQUIRED`, `RAFFLE_NOT_IN_PROGRESS` (`00`),
`PRIZE_NOT_DRAWN`, `PRIZE_ALREADY_REDEEMED`, `CONCURRENCY_CONFLICT` (`02`), `FILE_TOO_LARGE`,
`FILE_TYPE_NOT_ALLOWED`, `FILE_TYPE_MISMATCH`, `FILE_STORE_UNAVAILABLE` (ADR-0014), `DB_CONFLICT`
(the chassis).

## 9. English ↔ Spanish, and the frontend handoff

| Backend | Mock's Spanish |
| ------- | -------------- |
| `redemptionCode` | `codigoCanje` |
| `handover` / `redeemed` | `entrega` / `entregado` |
| `receiverName` | `receptorNombre` |
| `signature` (a file) | `firmaDigital` (a fixed string) |
| `handover/reverse` | `marcarPremioEntregado(…, false)`. Narrowed, see §3.3 |
| `NotarialRecord` | `acta notarial` |
| `reissue` | — |
| `pickupInstructions` | — |

Per Decision 26 a backend developer may wire this and **may not change any aspect of the UI**. What
the frontend owner has to absorb:

1. **The record is a stored file.** `ActaNotarialModal`'s jsPDF generation goes. The screen lists
   `notarialRecords` and downloads by `storedFileId`. It is labelled as the generated text, not the
   signed original (§4.1).
2. **Correcting an authority after `complete` is `reissue`**, with all six fields in one body.
3. **The handover needs a signature pad** that emits PNG, plus the receiver's name, their document,
   and "is this the winner?". "Confirm" stays disabled until the pad has ink. The ID checkbox
   becomes a document field.
4. **"Marcar entregado" and its un-tick go.** Every handover carries a signature, and undoing one is
   `reverse` with a reason.
5. **Codes come from the server.** The four client-side generators and placeholders
   (`mockSorteo.ts:536`, `CertificadoPremioModal.tsx:23-24`, `NotificarGanadorModal.tsx:27-28`,
   `EscanerCanjeModal.tsx:125,222`) go.
6. **The desk looks a code up with `GET /v1/prize-draws/by-code/{code}`**, and a `voided` answer
   is shown as such. Name search stays on the raffle screen.
7. **`NotificarGanadorModal` goes.** Email is automatic at `complete`, and WhatsApp is dropped
   (ADR-0022). The screen can show `winnerNotificationStatus` per draw.
8. **`pickupInstructions` is a new field in the raffle's `PATCH` body**, required before `complete`.
   ADR-0009's whole-body rule applies (`00` §12, item 2).
9. **The certificate**, if kept: no seal, no validator URL, no code (§6).

## Definition of done

- A draw writes a `redemption_code` matching `^RD-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$`.
  `System.Random` and the clock appear nowhere in its generation.
- The unique index on `prize_draw.redemption_code` exists in the migration set, **with no
  `raffle_id` in it**. Assert the constraint's columns, not only the behaviour.
- A void keeps the draw's code, and a redraw issues a different one.
- The lookup finds `RD-7K3M-Q9XA` from `rd7k3mq9xa`, from `RD 7K3M Q9XA`, from `7K3MQ9XA`, and
  `RD-1K30-Q9XA` from `rd-lk3o-q9xa`. Malformed input is `404 REDEMPTION_CODE_NOT_FOUND`. It answers `drawn`,
  `redeemed` and `voided` as `200`, an unknown code as `404 REDEMPTION_CODE_NOT_FOUND`, and changes
  no row. Its route has no name parameter.
- A handover with a PNG signature under 1 MB writes one `prize_handover`, and the prize reads
  `redeemed`. A JPEG, a PDF, a PNG over 1 MB, and a JPEG renamed `.png` are each refused with the
  seam's code, and **no `stored_file` row and no bytes remain**.
- A handover without `receiverIdentityDocument`, or without `receiverIsWinner`, is `400`, and so is
  one with `receiverIsWinner: true` still missing its document.
- A handover on a `completed` raffle succeeds, and so does one on a raffle whose event is `closed`.
- `reverse` keeps the row with all three reversal columns set and returns the prize to `drawn`. A
  following handover succeeds. A second live handover inserted directly is refused by the database.
- A void racing a handover on the same prize ends in one success and one `409 PRIZE_NOT_DRAWN` or
  `409 PRIZE_ALREADY_REDEEMED`, never a signature on a voided draw.
- Voiding a `redeemed` prize is still `409 PRIZE_ALREADY_REDEEMED`.
- `complete` without `pickupInstructions` is `409 RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED` and changes
  nothing. With a faulted file store (`FaultyFileStore`), `complete` is
  `503 FILE_STORE_UNAVAILABLE`, the raffle stays `in_progress`, and **no email is sent**.
- A successful `complete` writes `notarial_record` version 1, whose `stored_file` exists and whose
  content sniffs as PDF.
- **A golden test on the extracted text** of a record built from a fixed raffle proves each of §4.2's
  items is present, in order: both times from the data, the rules, every warning, the arithmetic
  summing, each manual decision naming its operator and **not its note**, a voided draw in the log
  **with its note**, a mid-evening prize marked, the annex numbered and alphabetical. It also asserts
  the absence of every email address, every redemption code, the words *sello* and *validador*, and
  any placeholder authority name.
- *(Amended by PGTI-275:)* a second golden test changes a rule between two draws, the first one
  voided, and asserts the changed-rules sentence in item 4 and each draw's own rules on its log line,
  in order. The first golden test keeps proving the single rules line.
- An operator id the realm cannot resolve still completes, and prints as *usuario no disponible*.
- `reissue` on an `in_progress` raffle is `409 RAFFLE_NOT_COMPLETED`. On a `completed` one it writes
  version 2, version 1's row and file still resolve, and the text of version 2 names version 1.
  `PATCH` on a `completed` raffle is still `409 RAFFLE_COMPLETED`.
- `complete` sends exactly one email per **live** draw, counted by `SendKeys` and not by HTTP
  requests (`events-service/CLAUDE.md`), with key
  `events:raffle:{raffleId}:draw:{drawId}:won`. A voided draw gets none. `reissue` sends none.
- With notification-service down, `complete` still succeeds and each draw records
  `winner_notification_status = failed`. This needs its own test factory, for the circuit breaker.
- An `en` event gets the `en` template, and an `fr` event gets `es`.
- `StoredFileReferences` lists `prize_handover` and `notarial_record`. The existing registry test
  covers it.
- No `raffle.acta_stored_file_id`, no redemption columns on `prize_draw`, no endpoint that redeems
  from a code lookup, and no raffle-specific file download route. Assert the absences.
