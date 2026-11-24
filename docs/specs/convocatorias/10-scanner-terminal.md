# The Scanner Terminal — Spec

The screen at the door that reads a code and says what it is: a ticket to check in, or a prize code
to look up. One screen, two kinds of code, and the operator never says which kind they are holding.

Almost everything the terminal does is already specified by the routes it calls. The CheckIn, its
idempotency and the door list are `09-check-in.md`. The redemption code, its lookup and the handover
are `docs/specs/raffles/03-evidence.md` §2-§3. This spec decides what is left: how a code is
recognised, who gets the prize path, how a terminal picks its Event, and what two terminals must
agree on. It adds **one backend route**. Everything else is frontend.

Settled elsewhere and not re-argued here: ADR-0025 (the fallback requires `platform:staff` or
`platform:service`, and `event_staff` holds neither), `09` §3.3 (nothing but convocatorias' own
database on the scan path), `09` §11 (no offline scanning, no per-OPT scoping of door staff),
`raffles/03` §2.4 (a lookup never redeems).

## Decisions behind this shape

Settled with the user on 2026-09-27 in a grilling session on PGTI-40.

- **Door staff check people in, and nothing else.** A prize code scanned under an `event_staff`
  login is recognised and turned away with no call. The prize path belongs to `raffles:draw`. §3.
- **The handover is built once, on the raffle screen.** The terminal identifies a prize code and
  links to it. It does not carry a second signature pad. §3.2.
- **The client recognises a code by its shape.** The two formats cannot be confused, so there is no
  trying one meaning and falling back to the other, and no server endpoint that tries both. §2.
- **A terminal picks its Event from a narrow list in events-service**, under `check-in:read`, of
  the Events happening around today. The only new route. §4.
- **Two terminals agree through the server and nothing else.** No shared feed, no push. §5.
- **C4 is not a redemption endpoint.** The plan's "prize redemption scan endpoint" would be a scan
  that redeems, which `raffles/03` §2.4 refuses. C4 is the door Event list instead. §8.

## 0. What exists today

`frontend/src/features/scanner/PwaScannerPage.tsx`, 947 lines, identical on `origin/frontend`. It
resolves everything in the browser against mock data.

| Fact | Where |
| ---- | ----- |
| Three tabs, *Todos (Auto)*, *Entradas*, *Premios* | `PwaScannerPage.tsx:29`, `:511-570` |
| Auto guesses by length: `RD-` or 10+ characters tries a prize first, else a ticket first | `:357-379` |
| A ticket matches on a substring of name or email, and silently switches to another Event | `:221-274` |
| A prize matches on a substring of code or **winner's name**, and is redeemed on the spot | `:277-337` |
| The redemption stores a fixed string as the signature | `:322-329`, `mockSorteo.ts:606-622` |
| The Event is a `<select>` over `useEventosList`, defaulting to the first | `:124-132`, `:590-604` |
| No camera. The viewfinder is decorative; *Escanear / Simular* picks a random row | `:387-418` |
| Check-ins live in component state and are lost on reload | `:121`, `:267` |
| Desktop browsers get a splash unless the URL has `?force=desktop` | `:421-468` |
| `/scanner` is outside `AppShell` and checks no role | `App.tsx:113-116` |

Every row above is replaced. Nothing is ported.

## 1. Vocabulary

No new glossary entry. The terminal is a screen, not a domain concept, and the words it needs
already exist: `CheckIn`, `QrCode` and `Reference` in `convocatorias-service/GLOSSARY.md`,
`RedemptionCode` and `Handover` in `events-service/GLOSSARY.md`.

**`Scan` stays on the avoid list** (`09` §1). The terminal scans; the domain never records one. This
spec says *scan* for the terminal's act and nothing else.

## 2. Recognising a code

The camera and the text box feed the same classifier. It runs in the browser, in this order, and
the first match wins:

| # | Input | Meaning | Call |
| - | ----- | ------- | ---- |
| 1 | A URL whose path ends in `/publico/entrada/{code}` | a ticket | `09` §3 with `{code}` |
| 2 | 43 characters of base64url, bare | a ticket | `09` §3 |
| 3 | `RD` then 8 Crockford base-32 characters, ignoring case, hyphens and spaces | a prize code | §3 |
| 4 | `MITUR-` then a four-digit year, a hyphen and digits | a Reference | none: opens the door-list search (§2.2) |
| 5 | anything else | — | none: *Código no reconocido*, with a button to the door-list search |

**The formats are disjoint by construction.** A QrCode is 32 CSPRNG bytes in base64url
(`RegistrationCodes.cs:63`), and the QR image encodes the ticket URL around it (`PortalLink.cs:58-64`).
A redemption code is `RD-XXXX-XXXX` from an alphabet with no `I L O U` (`raffles/03` §2.2). Neither
can be read as the other, so trying one meaning and falling back to the other only doubles the round
trips and puts both services on every scan at the door.

**No server endpoint tries both.** It would live in one service and call the other, which is the
cross-service edge `09` §3.3 keeps off the scan path.

**No tabs.** The operator never says what kind of code they hold. The mock's three tabs go.

### 2.1 The ticket URL

**The host is ignored.** Only the path is matched. A ticket from stage scanned on production is a
QrCode production has never seen, and `09` §3.4 already answers that with `404
CHECK_IN_CODE_NOT_FOUND`. Matching the host would need the portal's origin configured in the
frontend for no gain.

Stripping the URL is the terminal's job, as `09` §3 says, so the convocatorias contract never
learns the portal's routes.

### 2.2 A typed Reference

A Reference is quoted at a desk. It carries no authority (`09` §1), so typing one **never checks
anybody in**. It opens the door list (`09` §5) with the Reference in `search`, and the operator
checks the person in by id after looking at an identity document, the manual path `09` §5 already
defines.

### 2.3 Repeats

The camera reads the same QR many times a second while a phone is held up. **A code identical to
the last one read, within about two seconds, is dropped in the browser.** That is only to stop the
screen flickering. Correctness never depends on it: the scan is idempotent on the server (`09` §3.2).

## 3. Prize codes

### 3.1 Who gets the prize path

Decided by one role in the token, **`raffles:draw`**, the role a handover needs (`raffles/03` §3.1).

| The login | A prize code |
| --------- | ------------ |
| holds `raffles:draw` | the lookup, then the card below |
| does not (an `event_staff` login) | *Código de premio: diríjase al mostrador de premios.* No call |

**`event_staff` gets no prize role.** A lookup door staff cannot act on shows them winners' names
and nothing more. The prize desk is a coordinator's job, and a coordinator already holds every role
it needs. `09` §8.1 is corrected to match.

The lookup is `GET /v1/prize-draws/by-code/{code}` (`raffles/03` §2.4, built by PGTI-121). It is
a read with no policy of its own, so under ADR-0025 it needs `platform:staff`, which every
coordinator holds and `event_staff` does not. Door staff are shut out by the server as well as by
the screen.

### 3.2 The card

The terminal shows what the lookup returns: prize, winner name and company, and the status.

| `status` | The card says | Action |
| -------- | ------------- | ------ |
| `drawn` | the prize and its winner | **Registrar entrega**, a link to that prize's handover on the raffle screen |
| `redeemed` | who received it and when (`handover.receiverName`, `redeemedAt`) | none |
| `voided` | that the draw was declared void, and when (`void.voidedAt`) | none |
| `404 REDEMPTION_CODE_NOT_FOUND` | the code does not exist | none |

**The terminal never records a handover.** *Registrar entrega* navigates to the raffle screen at
`/sorteo/sorteos/{eventId}?entrega={prizeId}`, with the `eventId` and `prizeId` the lookup
returned, and the handover (receiver, identity document, `receiverIsWinner`, signature pad) happens
there. A handover is a raffle act with a signature pad and three refusals of its own (`raffles/03`
§3.4); two implementations of it would drift. **Amended 2026-10-23 (PGTI-132):** PGTI-125 settled
that route. A raffle is one per Event, so the link carries no `raffleId`.

**Nothing on the card is taken from the winner's name.** A lookup is by code only, never by name
(`raffles/03` §2.4).

## 4. Picking the Event

Every check-in route takes `{eventId}` (`09` §10), and a terminal is set to one Event (`09` §3). But
under ADR-0025 an `event_staff` login gets `403` from every events-service read and every
`convocatorias:read` route, so it cannot list Events to pick one. convocatorias-service cannot answer
either: it stores no Event, and its `IEventDirectory` only reads one by id or lists public ones.

So events-service, the owner, gets one narrow read:

```
GET /v1/events/door                                           check-in:read
```

```json
[
  { "id": "…", "name": "Feria de Turismo de Punta Cana",
    "startDate": "2026-10-03", "endDate": "2026-10-04",
    "cities": [ … ] }
]
```

| Rule | |
| ---- | - |
| **Which Events** | every Event whose `startDate − 1 day ≤ today ≤ endDate + 1 day` |
| **Visibility** | public and private alike. A private Event has a door too |
| **Status** | `cancelled` left out. Every other status in |
| **Sort** | `startDate`, then `name` |
| **Paging** | none. The window holds a handful |
| **Today** | the UTC date, as `RegistrationService.Today()` already takes it. The day of slack either side absorbs the Dominican Republic's four hours |

**The fields are the picker's and nothing more.** No budget, no OPT, no status, no counts. Door
staff see the name of tonight's Event, not the calendar.

**The window keeps door staff out of the calendar.** A day of slack either side covers setting up
the evening before and a night that runs past midnight.

**Leaving `cancelled` out is a courtesy to the picker, not a rule of the door.** A scan on a
cancelled Event still succeeds (`09` §3.3). The list gates nothing, and a terminal already set to an
Event that is then cancelled keeps working.

**`check-in:read` is a realm role, not a convocatorias one.** events-service registers a policy on
it like any of its own. Every staff composite that can check in can therefore also pick an Event,
and nothing else changes in the realm.

**This read is not on the scan path.** It is called when a terminal is set up, and events-service
being down then means the terminal cannot be set up, not that the door stops.

### 4.1 The terminal remembers

The chosen Event is kept in the device's `localStorage`, as a convenience. On reload the terminal
reads §4 again: if the stored Event is still in the list, its name comes back in the header; if it
has left the window, the terminal asks again.

**The Event's name is always in the header.** A terminal left on yesterday's Event must look wrong
to whoever picks it up. `09` §3.4's `409 CHECK_IN_WRONG_EVENT` is the backstop.

## 5. Two terminals at once

The server is the only thing two terminals share, and it already makes them agree:

| Race | Settled by |
| ---- | ---------- |
| One ticket scanned at two terminals | `09` §3.2: one `201`, one `200`, one CheckIn |
| One undo racing a scan | `09` §4: the undo clears both columns, the scan stamps them again |
| One prize handed over at two desks | `raffles/03` §3.4: the partial unique index, and the raffle row's stamp |

**What the second terminal needs to know is in the answer it gets.** A `200` with
`alreadyCheckedIn: true` and the first `checkedInAt` says "already came in at 18:42". A shared feed
of other terminals' scans would answer a question nobody has asked, and `09` §2 has no scan log to
read it from.

**Each terminal keeps its own recent reads**, in memory, for the operator's eye. They are the
terminal's, not the Event's.

**The footer total** (*Entradas: 287/412*) is `09` §5.1's `totals`, read after each CheckIn and on a
timer, about every 30 seconds. `totals` ignores the door list's filters, so any page of it will do.

No push, no WebSocket, no server-sent events. The platform has none, and nothing here needs one.

## 6. The camera

- **Real decoding, on iOS Safari and Android Chrome.** iOS Safari has no `BarcodeDetector`, so this
  needs a JavaScript decoder. **Amended 2026-10-23 (PGTI-132):** C5 uses the `barcode-detector`
  package, which is the native `BarcodeDetector` where the browser has one (Android Chrome) and
  zxing-wasm where it does not (iOS Safari).
- **The text box stays.** It is the typed path, and it is also how a USB or Bluetooth scanner that
  types like a keyboard reaches §2. No special support for one.
- **No offline.** A terminal with no network cannot check anybody in (`09` §11).

## 7. Frontend change requests

The frontend may lag and catch up in its own card (platform rule). In addition to `09` §9, which
already covers `event_staff` in `roles.ts`, the `/scanner` guard on `check-in:write` and the check-in
routes:

1. **The three tabs go.** One input, classified per §2.
2. **The camera works** (§6). The decorative viewfinder and *Escanear / Simular* go.
3. **The Event picker reads §4**, not `useEventosList`, remembers its choice (§4.1), and shows the
   Event name in the header. The mock's silent switch to another Event goes (`09` §3).
4. **Prize codes follow §3**: turned away without `raffles:draw`, the lookup card with it, and
   *Registrar entrega* links to the raffle screen. The mock's `canjearPremioConFirma` call goes.
5. **The desktop splash goes.** A prize desk is often a laptop or a tablet held sideways, and a
   coordinator must reach §3 from it.
6. **The bottom sheet** becomes `09` §5's door list for the chosen Event. It never lists prizes.
7. **Undo** (`09` §4) is offered on a checked-in row of the door list, and only there, behind a
   confirmation inside the app, never the browser's `window.confirm`. Amended 2026-10-23
   (PGTI-132).

## 8. Endpoints, roles and error codes

| Route | Role | § |
| ----- | ---- | - |
| `GET v{version}/events/door` | `check-in:read` | 4 |

**The only new route.** Everything else the terminal calls exists in another spec:

| Route | Spec |
| ----- | ---- |
| `POST /convocatorias/events/{eventId}/check-ins` | `09` §3 |
| `POST /convocatorias/events/{eventId}/check-ins/by-registration` | `09` §5 |
| `DELETE /convocatorias/events/{eventId}/check-ins/{registrationId}` | `09` §4 |
| `GET /convocatorias/events/{eventId}/registrations/door` | `09` §5 |
| `GET /prize-draws/by-code/{code}` | `raffles/03` §2.4 |

No new error codes.

**C4 was a "prize redemption scan endpoint".** Read literally, a scan that redeems, which
`raffles/03` §2.4 and §3 exist to refuse. The scanner needs nothing on the prize side beyond the
lookup and the existing handover, so C4 is the door Event list instead.

## 9. Not in this spec

- **A handover at the terminal.** §3.2. It lives on the raffle screen.
- **Prize lookup for door staff.** §3.1. Adding it later is one role on `event_staff`, and it
  should come with a reason.
- **A shared feed or live push between terminals.** §5.
- **Offline scanning, per-OPT scoping of door staff, registering at the door.** `09` §11.

## 10. Build slices

Two tickets under PGTI-33, created 2026-09-27.

| Ticket | Slice | Covers | Blocked by |
| ------ | ----- | ------ | ---------- |
| PGTI-131 | C4 | §4: `GET /v1/events/door`, the `check-in:read` policy in events-service | PGTI-127 |
| PGTI-132 | C5 | §2, §3, §5, §6, §7: the scanner frontend. Label `frontend`, `ready-for-human` | PGTI-131, PGTI-128, PGTI-121 |

C5 changes rendered output, so it belongs to the frontend owner (Decision 26).

## Definition of done

- `GET /v1/events/door` returns an Event starting tomorrow and one that ended yesterday, and not one
  that ended two days ago, one starting in two days, or a cancelled one inside the window.
- It returns private Events as well as public ones, and no field beyond `id`, `name`, `startDate`,
  `endDate` and `cities`.
- A token holding only `event_staff` gets `200` from it, and `403` from `GET /v1/events` and from
  `GET /v1/prize-draws/by-code/{code}`.
- The terminal checks a person in from the ticket URL's QR, from a bare QrCode, and from the door
  list, and never from a typed Reference.
- Under an `event_staff` login a prize code makes no network call.
- Under a coordinator login a `drawn` prize code shows the card and *Registrar entrega* reaches that
  prize's handover on the raffle screen, and a `voided` one says the draw was void.
- The camera decodes a ticket QR on iOS Safari and on Android Chrome.
