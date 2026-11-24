# convocatorias-service: check-in

> Spec: `docs/specs/convocatorias/09-check-in.md` (§3 the scan, §4 the undo, §5 the door list, §6 what
> coordinators see, §7 the raffle's read, §8 the roles). The scan and the undo were built in PGTI-127,
> the door list and check-in by registration id in PGTI-128, the `event_staff` composite for door
> staff in PGTI-129, and the raffle's read in PGTI-130.

## The scan

`POST v{version}/convocatorias/events/{eventId}/check-ins`, `check-in:write`, body `{ "qrCode": "…" }`.

- **The body carries the bare code.** The scanner strips the ticket URL; this route does not parse
  the portal's routes. The only shape check is `CheckInRequestValidator`: not empty, at most 256
  characters (`400`). Anything else that matches no Registration, a whole URL included, is
  `404 CHECK_IN_CODE_NOT_FOUND`.
- **The Event is in the path.** A code on another Event is `409 CHECK_IN_WRONG_EVENT`, and the
  problem names nobody: not the person, the reference or the other Event.
- **`201` the first time, `200` after, same body.** `alreadyCheckedIn` says which, and `checkedInAt`
  is always the first scan's. The `201` carries `Location: …/check-ins/{registrationId}`, the URL the
  undo deletes, built from the route values rather than the request path so `by-registration` points
  at the same place. **The body has no email.**
- **Idempotency lives in the `UPDATE`.** `CheckInRepository.StampAsync` sets the stamp only
  `WHERE checked_in_at IS NULL`. One row changed is a new CheckIn, zero is a repeat, so two
  terminals scanning at once get one `201` and one `200`. If an undo lands between the `UPDATE` and
  the re-read, the scan stamps once more rather than failing.
- **No call to events-service**, and no check of the Event's state (§3.3). A cancelled or closed
  Event does not stop the door, and an events-service outage cannot either.
- **Time comes from `TimeProvider`**, like every other stamp here. A CheckIn bumps `updated_at`.

## Check-in by registration id

`POST v{version}/convocatorias/events/{eventId}/check-ins/by-registration`, `check-in:write`, body
`{ "registrationId": "…" }`. For the person with no QR to show: staff find them in the door list,
look at an identity document and check them in by id.

- **A second way to find the Registration, not a second kind of CheckIn.** After the lookup it runs
  the scan's own tail in `CheckInService` (wrong Event, stamp, map), so the body, the `201`/`200`, the
  idempotency and the `Location` are the scan's.
- **Not found is `404 REGISTRATION_NOT_FOUND`; another Event is `409 CHECK_IN_WRONG_EVENT`**, naming
  nobody, as on the scan. A missing or empty id is `400` (`CheckInByRegistrationRequestValidator`).

## The door list

`GET v{version}/convocatorias/events/{eventId}/registrations/door?search=&checkedIn=&page=&pageSize=`,
`check-in:read`. Body `{ totals: { registered, checkedIn }, items, total, page, pageSize }`; each item
is `registrationId`, `firstName`, `lastName`, `company`, `reference`, `checkedInAt`.

- **No email in any item.** The email can be searched for but is never returned, and
  `DoorListEndpoints` maps field by field so the normalised address read for matching has nowhere to go.
- **The search runs in memory** (`Core/CheckIns/DoorListSearch`): one read of the Event's
  Registrations, then filter, sort and page in C#. No `unaccent`, no `ILIKE`, no migration. An Event
  is at most a few thousand rows.
- **Every word must match** the first name, the last name or the Reference, each word in any of the
  three, ignoring case and accents (`InvariantCulture`, `IgnoreCase | IgnoreNonSpace`, which needs
  ICU in the runtime image; `DoorListSearchTests` pins `garcia`/*García* and `nunez`/*Núñez*). The
  company is shown but not searched.
- **The whole trimmed input is also tried as an exact email**, normalised by
  `EmailAddressShape.Normalize` and compared with `email_normalized`. The two branches are OR'd;
  nothing detects whether the input looks like an address. A fragment of an address matches nothing.
- **`totals` ignores `search` and `checkedIn`**: it is the Event's footer (*Entradas: 287/412*).
- **Paging is this service's**: `page` below 1 is 1, `pageSize` defaults to 50 and is clamped to
  1..100, nothing answers `400`. Sorted by `lastName, firstName` (case- and accent-insensitive), then
  `reference`, which is unique, so the order is total.
- **Not `convocatorias:read`**, and not the participants list: no invitees who never registered, no
  send status, no referrers. `check-in:write` alone does not open it either.

## The undo

`DELETE v{version}/convocatorias/events/{eventId}/check-ins/{registrationId}`, `check-in:write`.

- **It clears both columns**, so a later scan makes a fresh CheckIn. `ck_registration_check_in_pair`
  keeps them both set or both null.
- **`204` whether or not there was a CheckIn**, so a double tap is not an error. A Registration
  that does not exist or is on another Event is `404 REGISTRATION_NOT_FOUND`.
- **The `UPDATE` names the CheckIn it read** (`checked_in_at` and `checked_in_by_id`). A racing undo
  plus re-scan therefore leaves the fresh CheckIn alone instead of clearing it under the old one's
  name.
- **One log line, not a table.** `CheckInService` logs, in Spanish at Information level, the
  registration id, the event id, the cleared `checked_in_at` and `checked_in_by_id`, and the `sub`
  that undid it. An undo that cleared nothing logs nothing.
- **It never reaches a started raffle**, which froze its list at `start`.

## The raffle's check-in list

`GET v{version}/convocatorias/events/{eventId}/check-ins`, `check-in:service-read`. Body
`{ "items": [...] }`; each item is `registrationId`, `emailNormalized`, `firstName`, `lastName`,
`company`, `contactId`, `origin`, `checkedInAt`. events-service's `ConvocatoriasAttendanceSource`
is the only caller.

- **Everyone with a CheckIn, unfiltered and unpaged**, ordered by `checkedInAt` then
  `registrationId`. No ceiling here: the raffle's 5,000 is the raffle's (raffles `01` §7).
- **The one check-in response that carries an email.** It is the raffle's identity key, and no
  composite holds `check-in:service-read`: only events-service's service account, assigned directly.
- **`origin` is `ParticipantOrigin.Of`**, the rule the participant merge calls: invited if the
  address is a recipient of one of the Event's launched campaigns (`LaunchedCampaign.Is`, whatever
  became of the send), else referred if the registration came through a recipient's link, else
  walk-in. `CheckInListRepository` reads the inputs and never re-derives the rule in SQL (§7.1).
- **`contactId` is the registration's own `crm_contact_id`**, never the forwarding recipient's, and
  null while the CRM write is pending.
- **Always `200`.** An Event with no check-ins, or one this service never heard of, is `items: []`.
  No call to events-service, which is the caller.
- Its own endpoint class, `CheckInServiceReadEndpoints`: in `CheckInWriteEndpoints`' group on the same
  path it would need `check-in:write` as well.

## Where things are

| What | Where |
| ---- | ----- |
| The decisions (not found, wrong Event, new or repeat, the undo log) | `Core/CheckIns/CheckInService.cs` |
| The conditional `UPDATE`s, the re-read and the lookups by code and by id | `Infrastructure/Database/CheckInRepository.cs` |
| The write routes, `201`/`200` and `Location` | `Web/CheckInEndpoints/CheckInWriteEndpoints.cs` |
| The door list: search, totals, sort and paging | `Core/CheckIns/DoorListSearch.cs`, `Core/CheckIns/DoorListService.cs` |
| The door list's read and route | `Infrastructure/Database/DoorListRepository.cs`, `Web/CheckInEndpoints/DoorListEndpoints.cs` |
| The raffle's read and route | `Infrastructure/Database/CheckInListRepository.cs`, `Web/CheckInEndpoints/CheckInServiceReadEndpoints.cs`; the origin rule is `Core/Participants/ParticipantOrigin.Of` |
| The roles | `Web/Auth/AuthorizationPolicies.CheckInWrite`, `.CheckInRead` and `.CheckInServiceRead`; realm JSON plus the console steps in `docs/deploy-with-dokploy.md`. That door staff reach these four routes and nothing else: `DoorStaffAccessTests` in each service's integration tests |
| What coordinators see of it (`checkedIn`, `checkedInAt`, the CSV column) | `docs/tracking.md` |
| The contact-history read (PGTI-170), unrelated to the door | `IContactCheckInRepository`, `docs/tracking.md` |
