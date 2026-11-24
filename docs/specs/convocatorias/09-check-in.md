# Check-in — Spec

Admitting a registered person at the door of an Event, and handing the raffle the list of who was
admitted.

A CheckIn is the only way into a raffle's drum (ADR-0022), so the door stopped being a nicety and
became the start of a legal record. The scan is idempotent because a door is a queue of phones
being waved twice. It depends on nothing but this service's own database, because an Event night is
the worst possible time to discover that another service is down.

Settled elsewhere and not re-argued here: ADR-0022 (the raffle lives in events-service and reads
attendance through a seam), `docs/specs/raffles/01-qualification.md` (what the raffle does with the
list), `06` (the Registration, its Reference and its QrCode), `07` (the tracking read), ADR-0016
(the realm JSON seeds, the console owns). The scanner terminal itself is `10-scanner-terminal.md`
(PGTI-40).

## Decisions behind this shape

Settled with the user on 2026-09-26 in a grilling session on PGTI-39.

- **The word is `CheckIn`**, es. *check-in*. It names the act at the door, not a kind of person, so
  the ban on `Attendance`, `Attendee` and `Asistente` in `GLOSSARY.md` stands. §1.
- **Two columns on `registration`, stamped once.** No table, no scan log. §2.
- **A CheckIn can be undone**, by whoever can make one, and an undo never reaches a raffle that has
  started. §4.
- **The QrCode is the credential, and there is a manual path beside it**: door staff search the
  Event's registrations and check a person in by id after looking at an identity document. §5.
- **No check of the Event's state, and no call to events-service, on the scan path.** §3.3.
- **Referred people are eligible for the raffle; only walk-ins are out.** A travel agent who
  forwards the invitation to a colleague at the same agency has brought someone MITUR wants in the
  room. The raffle spec is amended to match. §7.2.
- **A registrant with no CRM contact is not qualified**, because the blacklist cannot be asked
  about them, and a manual include cannot override that. §7.2.
- **Door staff get a composite of their own, `event_staff`**, and it may not ship until the
  fallback policy in events-service and crm-service stops admitting any signed-in user. ADR-0025.
  §8.

## 0. What exists today

Registration shipped with PGTI-56, PGTI-58 and PGTI-61. The parts this spec builds on:

| Fact | Where |
| ---- | ----- |
| `registration` has no status column. Registered means the row exists | `Registration.cs:8-10` |
| `qr_code` is 32 CSPRNG bytes in base64url, 43 characters, unique | `RegistrationCodes.cs:63`, `ux_registration_qr_code` |
| The QR image encodes a URL, `{portal}/publico/entrada/{qr_code}`, not the bare code | `PortalLink.cs:58-64` |
| `reference` is `MITUR-<yyyy>-<seq D5>`, unique, from one Postgres sequence | `RegistrationCodes.cs:42-50` |
| `FindByQrCodeAsync` exists. There is no lookup by `reference` | `IRegistrationRepository.cs:51` |
| `crm_contact_id` is the registrant's own contact, null while the CRM write is pending | `06` §4.2, `07` §6.1 |
| Stamp-if-null with a re-read is an established pattern | `ReconciliationRepository.cs:102-135` |
| Every internal route carries an explicit role. The fallback is any signed-in user | `AuthorizationPolicies.cs:56-62` |

Nothing about check-in exists in code: no column, no route, no role. **Amended 2026-10-03:** the
§2 columns, the check and the partial index shipped with PGTI-170, which reads check-ins by CRM
contact; nothing writes them yet, and the scan (PGTI-127) needs no migration for them.

## 1. Vocabulary

Recorded in `convocatorias-service/GLOSSARY.md`.

| Identifier | Spanish | Meaning |
| ---------- | ------- | ------- |
| `CheckIn` | *check-in* | the holder of a Registration was admitted at the door. At most one per Registration at a time |
| `QrCode` | *código QR* | the credential presented at the door. Scanning it is how a CheckIn happens |
| `Reference` | *código de registro* | quoted at a desk and searched for. Carries no authority, and still does not |

**A scan is not a CheckIn.** A scan is the terminal's act: it can fail, and it repeats. A CheckIn
happens once. The word `Scan` is on the avoid list for that reason.

**The raffle says *attendee*.** `01` §1 uses it for a checked-in person, on events-service's side of
the boundary. It never crosses back: this service's routes, fields and types say `checkIn`.

## 2. What is stored

Two nullable columns on `registration`, added by one migration:

| Column | Notes |
| ------ | ----- |
| `checked_in_at` | `timestamptz`. Null means not checked in |
| `checked_in_by_id` | the `sub` of whoever made the CheckIn. Null exactly when `checked_in_at` is |

Plus a partial index `ix_registration_event_checked_in` on `(event_id) WHERE checked_in_at IS NOT
NULL`, which serves §6's count and §7's list.

**No table.** A `check_in` table with a unique `registration_id` would mirror "registered means the
row exists", but it would hold two columns and a foreign key to say what two columns already say.

**No scan log.** Recording every presentation of a code, repeats and failures included, would answer
"how many times was this QR shown", and nobody has asked that. The raffle's evidence trail needs
when a person came in, and `checked_in_at` is that.

## 3. The scan

```
POST /v1/convocatorias/events/{eventId}/check-ins         check-in:write
{ "qrCode": "q8Zr…43 chars" }
```

**The body carries the bare code.** The camera reads the whole ticket URL; stripping it is the
scanner's job (PGTI-40). Parsing the portal's URL shape here would make the portal's routes part of
this contract.

**The Event is in the path** even though the QrCode alone identifies the Registration. A terminal is
set to one Event, and a ticket for tomorrow's Event is a mistake the door should catch rather than
act on. The mock scanner's habit of finding the code on another Event and switching to it silently
is exactly what this forbids.

### 3.1 The answer

The same body in both cases:

```json
{
  "registrationId": "…",
  "firstName": "María", "lastName": "García",
  "company": "Viajes del Caribe SRL",
  "reference": "MITUR-2026-00427",
  "checkedInAt": "2026-10-03T18:42:11Z",
  "checkedInById": "…",
  "alreadyCheckedIn": false
}
```

| Case | Status | `alreadyCheckedIn` | `checkedInAt` |
| ---- | ------ | ------------------ | ------------- |
| First scan | `201` | `false` | now |
| Any later scan | `200` | `true` | **the first scan's time** |

Returning the original time is what lets door staff say "you came in at 18:42" rather than just "no".
The status mirrors public registration's `201`/`200` (`06` §4), and the flag is in the body so the
frontend never has to branch on a status code.

**No email in the body.** Door staff compare a name with an identity document. An address adds
nothing to that and would put an Event's worth of emails on a door terminal.

### 3.2 Idempotency

```sql
UPDATE registration
   SET checked_in_at = now(), checked_in_by_id = @sub
 WHERE id = @id AND checked_in_at IS NULL
```

One affected row is a new CheckIn and answers `201`. Zero is a repeat: re-read the row and answer
`200` with the stamp that won. **Two terminals scanning the same code at the same instant get one
`201` and one `200`**, never two CheckIns and never an error. This is the pattern
`AcknowledgeDetailsAsync` already uses.

### 3.3 No gating on the Event

The scan does not ask events-service whether the Event is cancelled, closed or happening today.

- Every such check puts events-service on the scan path, so an outage there stops the door.
- A cancelled Event with people scanning in at its door is not a failure anybody expects.
- A CheckIn on a closed Event is harmless. The raffle froze its list when it started (`01` §4) and
  never reads the door again.
- The RegistrationWindow is about new Registrations. A CheckIn is about one that already exists.

### 3.4 Refusals

| Case | Answer |
| ---- | ------ |
| No Registration has that QrCode | `404 CHECK_IN_CODE_NOT_FOUND` |
| The QrCode belongs to a Registration on another Event | `409 CHECK_IN_WRONG_EVENT` |
| Body missing, code empty, or code longer than 256 characters | `400`, the platform validator |

**Amended 2026-10-08 (PGTI-127): the code is capped at 256 characters**, and that is the only shape
check. Anything else that matches no Registration, a whole ticket URL included, is
`404 CHECK_IN_CODE_NOT_FOUND`: the scanner owns the parsing, and this route does not second-guess it.

`CHECK_IN_WRONG_EVENT` carries **nothing about the other Registration**: not its name, not its
Event. The person at the door needs to hear "this is not a ticket for tonight", and that is all.

## 4. Undo

```
DELETE /v1/convocatorias/events/{eventId}/check-ins/{registrationId}      check-in:write
```

The case it exists for: two people called María García registered, one came, and the one picked
from the list (§5) was the other. Without an undo, the door count is wrong forever and the raffle
contains an absent person unless somebody remembers to tell the operator.

- **It clears both columns**, so a later scan makes a fresh CheckIn. **Amended 2026-10-08
  (PGTI-127):** an undo that actually cleared a CheckIn writes one structured log line, in Spanish,
  with the registration id, the event id, the cleared `checked_in_at` and `checked_in_by_id`, and
  the `sub` of whoever undid it. No table and no column: the database still forgets, the log
  remembers who made the mistaken CheckIn and who fixed it. An undo that cleared nothing logs
  nothing.
- **`204` whether or not there was a CheckIn to clear**, so a double-tapped undo is not an error.
- **`404 REGISTRATION_NOT_FOUND`** when the Registration does not exist or is not on this Event.
- **Held by `check-in:write`, not by coordinators alone.** The mistake happens at the door, the
  coordinator is often not there, and the person who just made it is the one best placed to fix it.

**An undo never reaches a started raffle.** The raffle freezes its list at `start` and reads only
its own table afterwards (`01` §4). An undo after that corrects the door's record and leaves the
snapshot alone, which is what the snapshot is for. A person wrongly in a started raffle is handled
there: if drawn, the prize is voided as `absent` (`raffles/02` §3).

## 5. The door list

The manual path. For the person whose phone is dead or who never received the email: staff search,
look at an identity document, and check them in by id.

```
GET  /v1/convocatorias/events/{eventId}/registrations/door?search=&checkedIn=&page=&pageSize=
                                                                            check-in:read
POST /v1/convocatorias/events/{eventId}/check-ins/by-registration          check-in:write
{ "registrationId": "…" }
```

The second route answers exactly as §3 does, with the same body, the same `201`/`200` and the same
idempotency. It is a second way to identify the Registration, not a second kind of CheckIn.

**Amended 2026-10-10 (PGTI-128):** a `201` from either write route carries
`Location: /v1/convocatorias/events/{eventId}/check-ins/{registrationId}`, the URL §4's undo deletes.

### 5.1 The read

```json
{
  "totals": { "registered": 412, "checkedIn": 287 },
  "items": [
    { "registrationId": "…", "firstName": "María", "lastName": "García",
      "company": "Viajes del Caribe SRL", "reference": "MITUR-2026-00427",
      "checkedInAt": null }
  ],
  "total": 2, "page": 1, "pageSize": 50
}
```

| Param | Behaviour |
| ----- | --------- |
| `search` | substring over first name, last name and Reference, **or** exact match on the normalised email (amended below) |
| `checkedIn` | `true` / `false` |
| `page`, `pageSize` | the platform validator, cap 100 (amended below). Sort `lastName, firstName, reference` |

**Amended 2026-10-10 (PGTI-128):**

- **`search`** is split on whitespace and every word must appear in the first name, the last name or
  the Reference (each word may match a different field), ignoring case and accents: `garcia` finds
  *García*, `garcía 00427` finds her too. The whole trimmed input is also tried, as a second branch
  OR'd with the first, as an exact match on the normalised email. Nothing guesses whether the input
  looks like an address.
- **Paging follows this service's lists, not a validator:** `page` below 1 is 1, `pageSize` is
  clamped to 1..100 with a default of 50, and an out-of-range number never answers `400` (a value
  that is not a number still fails binding, as on every list here). The count field is `total`, as
  on every other paged list here, not `totalCount`.

**Email can be searched but is never returned.** Staff type an address the person gives them; they
never see a list of addresses. It is an **exact** match rather than a substring match, so the
search box cannot be used to enumerate addresses a letter at a time.

**`totals` ignores the filters.** It is the Event's count, for the scanner's footer (*Entradas:
287/412*), and it is the same whatever is typed in the box.

**This is not `07`'s participant list.** That one is a coordinator's read under
`convocatorias:read`: it merges invitees who never registered, shows emails, send status and
referrers, and exports to CSV. The door needs none of that, and giving door staff
`convocatorias:read` would give them all of it.

## 6. What coordinators see

`07` gains two fields, both computed from §2's columns:

- **The rollup** (`07` §2): `registrations.checkedIn`, the number of Registrations on the Event with
  a CheckIn.
- **The participant row** (`07` §3): `checkedInAt`, null on a row with no Registration or no
  CheckIn.

No-show is registered minus checked in. It gets no field and no glossary entry: the frontend can
subtract, and a stored no-show count would be one more number that could disagree with the others.

## 7. The raffle's check-in list

The seam `01` §7 names `ConvocatoriasAttendanceSource`. This is the route it calls.

```
GET /v1/convocatorias/events/{eventId}/check-ins          check-in:service-read
```

```json
{
  "items": [
    { "registrationId": "…",
      "emailNormalized": "maria@viajesdelcaribe.com",
      "firstName": "María", "lastName": "García",
      "company": "Viajes del Caribe SRL",
      "contactId": "…",
      "origin": "referred",
      "checkedInAt": "2026-10-03T18:42:11Z" }
  ]
}
```

- **Everyone with a CheckIn on the Event, unfiltered and unpaged.** Walk-ins, `.do` addresses, people
  CRM has blacklisted: all of them. Classifying them is the raffle's job (`01` §7), and it needs the
  full number to make `checked_in = qualified + not_qualified` hold.
- **No ceiling here.** `01` §7's limit of 5,000 attendees is the raffle's constraint, not this
  service's. The raffle refuses above it with
  `ATTENDANCE_TOO_MANY_ROWS`. A check-in list cannot outgrow the Event's Registrations anyway.
- **Ordered by `checked_in_at`, then `registrationId`**, so two calls over the same data return the
  same order.
- **Only events-service's service account calls it.** No composite holds `check-in:service-read`. A
  person looking at the door uses §5.

### 7.1 `origin` and `contactId`

**`origin` is the value `07` §3 already computes**: `invited`, `referred` or `walk_in`, derived the
same way and by the same code. The raffle used to test "no `campaign_recipient_id`" itself. That
re-derived a convocatorias concept on the other side of a service boundary, and it got referrals
wrong (§7.2).

**`contactId` is the registrant's own `crm_contact_id`**, never the contact of the CampaignRecipient
whose link they used. For a referred person that recipient is somebody else, the colleague who
forwarded the email, and running the blacklist against them would clear the wrong person. It is null
while the registration's CRM write is pending (`07` §6.1).

### 7.2 What the raffle does with it

Amended in `01` in the same PR as this spec:

| `origin` | In the drum? |
| -------- | ------------ |
| `invited` | yes, subject to the rest of the pipeline |
| `referred` | **yes**, subject to the rest of the pipeline |
| `walk_in` | no, and no manual include can admit them (`01` §2, unchanged) |

| `contactId` | Result |
| ----------- | ------ |
| present | the blacklist is asked about this person |
| null | not qualified, `contact_unresolved`, and no manual include can admit them. Retrying the CRM write from the reconciliation screen (`07` §6.1) brings them back at the next compute |

### 7.3 The edge

This is the first call from events-service to convocatorias-service. `GLOSSARY-MAP.md` writes edges
caller first, and its entry "Events → Convocatorias — none, deliberately" becomes **Events →
Convocatorias — the check-in list for the raffle, and nothing else**. What that entry was always
about still holds: `InvitedCount` stays manual, and an Event read never waits on convocatorias,
because this call is made by a raffle, not by an Event read.

The client follows the platform rule: `AddServiceClient` from the chassis, events-service's own
client-credentials token, `Services__Convocatorias__BaseUrl` in **both** compose files.

## 8. Roles and the realm

### 8.1 The roles

| Role | Held by | Guards |
| ---- | ------- | ------ |
| `check-in:write` | `opt_coordinator`, `opt_director`, `central_admin`, `event_staff` | §3, §4, §5's `POST` |
| `check-in:read` | `opt_coordinator`, `opt_director`, `central_admin`, `event_staff` | §5's `GET` |
| `check-in:service-read` | `service-account-events-service`, assigned directly | §7 |

**`event_staff`** is a new composite, es. *personal de la puerta* (**amended 2026-10-12, PGTI-129**: was
*personal del evento*, which reads as everyone working the Event; see DoorStaff in `GLOSSARY.md`), holding `check-in:write` and
`check-in:read` **and nothing else**. The person at the door must not be an `opt_coordinator`, which
can create Events and delete CRM contacts. **It gets no prize role**: a prize code scanned under an
`event_staff` login is turned away with no call, and the prize path belongs to `raffles:draw`
(`10-scanner-terminal.md` §3.1, amended 2026-09-27). The same `check-in:read` also
opens events-service's door Event list (`10` §4).

**Directors and central admins get check-in too** because they already hold every convocatorias role
(`00` §2). Leaving them out would be an accident, not a decision.

### 8.2 The blocker: the fallback policy

An `event_staff` login is a signed-in user, and **every read in events-service and crm-service
admits any signed-in user**. Door staff would be able to read every CRM contact, every company,
every quote, and, once the raffle ships, every winner's identity-document number and signature
through `GET /v1/files/{id}/content` (`raffles/03` §7). They could also create and edit CRM tasks,
whose writes carry no role.

ADR-0025 closes this. The fallback in each service stops meaning "signed in" and starts meaning
"holds `platform:staff` or `platform:service`". `event_staff` holds neither, so everything not
explicitly granted to it is shut, including endpoints nobody has written yet.

**`event_staff` must not be created in any environment until ADR-0025 is live there.** Creating the
composite first, even on stage, opens every CRM contact to a door login for as long as the gap lasts.

### 8.3 Realm JSON and console

Per ADR-0016 every change lands twice in the same PR: in `infra/keycloak/pgti360-realm.json`, and as
a console step in `docs/deploy-with-dokploy.md`. The steps, in order:

1. **Create three realm roles**: `check-in:write`, `check-in:read`, `check-in:service-read`.
   **Amended 2026-10-08 (PGTI-127): each role lands with the ticket whose route first uses it**,
   not all three at once: `check-in:write` with PGTI-127, `check-in:read` with PGTI-128,
   `check-in:service-read` with PGTI-130. Steps 2 and 3 follow the same split.
2. **Add `check-in:write` and `check-in:read`** to `opt_coordinator`, `opt_director` and
   `central_admin`.
3. **Assign `check-in:service-read`** to `service-account-events-service`, and make sure its tokens
   carry `convocatorias-api` in `aud`, the way convocatorias-service's tokens already carry the
   audiences it calls.
4. **Create the `event_staff` composite** with `check-in:write` and `check-in:read`. Only after
   ADR-0025's console steps are done on that realm.

Verification on the console: `event_staff` → *Associated roles* lists exactly two roles, and neither
`platform:staff` nor `platform:service` is among them.

## 9. Frontend change requests

The frontend may lag and catch up in its own card (platform rule). These are the requests, and none
blocks the backend.

**Status, 2026-10-23:** item 2 is done (PGTI-219). Items 1 and 3 are PGTI-132's. Item 4 moved to
PGTI-294.

1. **`roles.ts` learns `event_staff`**, and a user holding only it lands on the scanner, not on a
   dashboard.
2. **`roles.ts:197` stops returning `true` for `sorteo` unconditionally.** Today every signed-in
   user, door staff included, can open `/sorteo/*`, winners' history page included.
3. **`/scanner` gets a guard** on `check-in:write`. Today it has none (`App.tsx:114`).
4. **`AppShell.tsx:70-74` stops calling `useCotizacionesPendientes()` and
   `useEventosProximosACerrar()` for users without the Eventos module.** After ADR-0025 those calls
   answer `403` for door staff instead of quietly succeeding.
5. **The check-in screens read the real routes**: §3 for a scan, §5 for the list and the footer
   totals, §4 for undo, §6 for the coordinator's KPIs. The mock's substring search on name and email
   becomes §5's `search`; its JSON QR payload `{ticket_id, participant_id, …}` is gone, because the
   real QR is the ticket URL.

## 10. Endpoints, roles and error codes

| Route | Role | § |
| ----- | ---- | - |
| `POST /events/{eventId}/check-ins` | `check-in:write` | 3 |
| `POST /events/{eventId}/check-ins/by-registration` | `check-in:write` | 5 |
| `DELETE /events/{eventId}/check-ins/{registrationId}` | `check-in:write` | 4 |
| `GET /events/{eventId}/registrations/door` | `check-in:read` | 5 |
| `GET /events/{eventId}/check-ins` | `check-in:service-read` | 7 |

All under `v{version:apiVersion}/convocatorias/`. Plus two fields on `07`'s existing reads (§6).

| Code | Kind | When |
| ---- | ---- | ---- |
| `CHECK_IN_CODE_NOT_FOUND` | NotFound | no Registration has that QrCode |
| `CHECK_IN_WRONG_EVENT` | Conflict | the QrCode or registration id belongs to another Event |
| `REGISTRATION_NOT_FOUND` | NotFound | §4 or §5's `POST` names a Registration that does not exist. New in PGTI-127; unrelated to the portal's `PUBLIC_REGISTRATION_NOT_FOUND` |

`by-registration` naming a Registration on another Event answers `CHECK_IN_WRONG_EVENT`, as a scan
would.

## 11. Not in this spec

- **The scanner terminal**: its camera, its modes, prize codes, and what two terminals must agree
  on. `10-scanner-terminal.md` (PGTI-40).
- **Registering someone at the door.** A person with no Registration registers on the public portal
  from their own phone, on a public Event, and then scans. A private Event has no walk-up path, by
  design.
- **Offline scanning.** A terminal with no network cannot check anyone in. Queueing scans on the
  device and replaying them later is a different design, with its own conflict rules, and nobody has
  asked for it.
- **Per-OPT scoping of door staff.** An `event_staff` login can check people in at any Event. The
  `opt_code` claim is still read by nothing (`README`, out of scope).

## 12. Build slices

Five tickets under PGTI-33, created 2026-09-26. The plan's Phase 6 carries the same list.

| Ticket | Slice | Covers | Blocked by |
| ------ | ----- | ------ | ---------- |
| PGTI-126 | Fallback flip | ADR-0025 in all four services, `platform:staff` and `platform:service` in the realm and console | — |
| PGTI-127 | C1 | §2, §3, §4, `check-in:write` (only) on the three staff composites, §8.3 | — |
| PGTI-128 | Door list | §5, §6, `check-in:read` on the three staff composites | PGTI-127 |
| PGTI-129 | C2 | `event_staff`, §8.3 step 4 | PGTI-126, PGTI-127, PGTI-128 |
| PGTI-130 | C3 | §7, `check-in:service-read`, the raffle replaces its placeholder | PGTI-127, PGTI-114 |

C4 (the door Event list, PGTI-131) and C5 (the scanner frontend, PGTI-132) are `10-scanner-terminal.md` §10.

## Definition of done

- **Scanning the same code twice succeeds both times**: `201` then `200`, `alreadyCheckedIn` true
  on the second, and `checkedInAt` equal on both. The integration test the whole endpoint exists for.
- Two concurrent scans of one code produce exactly one CheckIn, one `201` and one `200`.
- A QrCode from another Event answers `409 CHECK_IN_WRONG_EVENT` and its body names nobody.
- An undo followed by a scan produces a fresh CheckIn with a new time. An undo on a Registration
  with no CheckIn answers `204`.
- The door list finds a person by an exact email and not by a fragment of one, and no response
  under `check-in:read` or `check-in:write` contains an email.
- A token holding only `event_staff` gets `403` from every `convocatorias:read` route, and, once
  ADR-0025 is live, from events-service's and crm-service's reads.
- The check-in list returns a walk-in, a referral and an invitee with the `origin` `07` gives each,
  and a referral's `contactId` is the registrant's own, not the forwarding invitee's.
- The realm JSON diff and the `docs/deploy-with-dokploy.md` console steps land in the same PR as the
  code that needs them.
