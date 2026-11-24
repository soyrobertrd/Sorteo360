# The Registration Portal — Spec

What convocatorias exposes to the open internet, and what stops it being abused. Seven anonymous
endpoints, one credential, three rate-limit axes. Plus §5's registration window, whose two endpoints
are authenticated but whose whole effect is on these seven.

This is the surface that kept the service standalone, so it deserves the most care. Every other
endpoint on this platform sits behind Keycloak. These do not: a stranger clicks a link in a forwarded
email and posts a form.

`00` §4 and §5 already fix how these routes reach the internet and how the split is enforced. This
spec is what they do.

## 1. The endpoint set

All under `v{version:apiVersion}/public/`, all anonymous, all rate limited, all reachable only
through the two nginx locations in `00` §4.

| Route | Body / params | Returns | Failure |
| ----- | ------------- | ------- | ------- |
| `GET /public/events` | `page`, `pageSize` capped at 50 | the `01` §5 list shape, each item carrying `registrationOpen` | an empty page, never `404` |
| `GET /public/events/{id}` | | the same fields plus `description`, the OPT name and `registrationOpen` | `404` if not found, not public, or cancelled, one code for all three |
| `POST /public/registrations/resolve` | `{ token }` | the event, plus `prefill` **only when the event is private** | `404`, one code for unknown, spent, revoked or cancelled-event. `409 REGISTRATIONS_CLOSED` when the window is shut, §5 |
| `POST /public/registrations` | `{ eventId, token?, firstName, lastName, email, phone, company, jobTitle, country }` | `201 { reference, qrCode, event }` | `404` on a bad token, `409 REGISTRATIONS_CLOSED` when the window is shut. **No `409` for a duplicate**: a repeat returns `200` with the existing registration |
| `GET /public/registrations/{qrCode}` | | the ticket: reference, event, name, company | `404` unknown |
| `GET /public/registrations/{qrCode}/qr` | | `image/png`, generated on demand | `404` unknown |
| `POST /public/unsubscribe` | `{ token }` | the masked address it acted on | `404` unknown token |

`429` carries `Retry-After` and a machine `code` on all of them, so the portal renders "demasiados
intentos, espera un minuto" rather than a generic failure.

**No CORS policy anywhere.** Every call is same-origin through nginx. Adding one is what would make
cross-origin abuse possible, not what would prevent it.

### 1.1 Registering with no token at all

`POST /public/registrations` takes an **optional** token, not two endpoints.

- **Token present**: validate it, bind the referral, and reject if the event is private and the token
  is already spent.
- **Token absent**: accept only if the event is public. `campaign_recipient_id` stays null, which is
  the nullable link the model was designed around.

Roberto's portal lists public events and anyone can register from there without ever receiving an
email, so the walk-in path is not optional. Two endpoints would duplicate every validation and every
rate limit for the sake of one nullable field.

## 2. Tokens and codes

### 2.1 The registration token

**The only credential.** 32 bytes from `RandomNumberGenerator`, base64url, 43 characters.

- **Stored as a SHA-256 hash**, never in plaintext, looked up by hash with a constant-time
  comparison. The link is built at send time and the plaintext is never needed again, so a database
  dump hands over no working links.
- **No expiry column.** Valid while the event is neither cancelled nor past its end date. An expiry
  field invites an expiry policy nobody asked for.
- **Never in a URL query string.** The email links to
  `https://app.${DOMAIN}/publico/registro#t=<token>`. Browsers do not send a fragment to the server,
  so the token reaches no access log anywhere. Serilog logs `Request.Path` without the query, but
  nginx and Traefik access logs record the full request line, on disk, on stage and on whatever the
  client's network team runs in production. **This is why resolve is a `POST` with the token in the
  body.**

### 2.2 What resolution reveals

**Prefill only for private events.** A private token is bound to one invitee and arrived at that
address, so returning their name and email leaks nothing. A public token is designed to be forwarded,
and prefilling on a forwarded link would show the forwardee the original invitee's name and address.

Public token resolution returns the event and **no invitee identity at all**. Nothing about a public
event is secret in the first place; the invitee is the only thing worth protecting here.

**Resolution records nothing.** There is no row to record a referral on until somebody submits, so the
referring recipient is written on the `registration` itself, as `campaign_recipient_id` (§4). A public
token resolved and never submitted leaves no trace, by construction rather than by omission.

### 2.3 Two codes on a registration, with different jobs

| Field | Shape | Job |
| ----- | ----- | --- |
| `reference` | `MITUR-2026-00427`, short, human | printed on the badge, said over the phone. **Carries no authority** |
| `qr_code` | 32 random bytes, base64url | what the QR encodes and what the QR URL is keyed on. Never shown as text |

One opaque code became a credential the moment it entered a public URL, and 43 base64url characters
is not something a person reads off a badge. Collapsing the two means either an unreadable badge or a
guessable credential.

**Accepted consequence**: `qr_code` does appear in access logs, because an `<img src>` in an email
must be a fetchable GET URL and there is no fragment trick for that. It is a lower-value secret than
the token. Note for the out-of-scope check-in scanner: presenting a QR must not be the only proof of
identity at the door.

### 2.4 The unsubscribe token

**A separate `unsubscribe_token` on `campaign_recipient`**, same shape and same hashing as §2.1,
delivered as `#u=<token>`.

Reusing the registration token was rejected on one case: a public registration token is designed to
be forwarded, so one token means the forwardee's unsubscribe click opts out the original invitee.

A separate token does not fully close that, because the forwardee receives the whole email including
the unsubscribe link. So **the confirm page names who it is about, masked**: `dar de baja a
a***@acme.com`. The forwardee sees an address that is not theirs and stops.

**The click is a GET, the opt-out is a POST.** The link renders a confirmation page; the button posts.
A corporate mail scanner prefetching every link in a message must not silently unsubscribe an invitee
list. RFC 8058 one-click `List-Unsubscribe` is the opposite bet, it is the sending gateway's header to
set, and it is out of scope here.

## 3. Abuse controls

Three axes, all on the built-in ASP.NET rate limiter, configured in `00` §6.

| Axis | Applies to | Catches |
| ---- | ---------- | ------- |
| Per IP, fixed window | the whole public prefix | ordinary flooding |
| Per token | resolve, unsubscribe | enumeration that rotates IPs |
| Per email | registration submit | using our portal to mail-bomb a victim by resubmitting their address |

`POST /public/unsubscribe` takes the first two axes and **not** the per-email one: unlike
registration it sends no mail, so there is nothing to bomb anyone with, and no email is sent after
unsubscribing either. The page confirms it.

**No CAPTCHA in phase 1, and the question is not put to the client yet.** It means embedding Google or
hCaptcha JavaScript in a `gob.do` portal, which is a data-protection conversation, and there is no
abuse to point at.

The thing that must never live in memory is §4.1's uniqueness rule, which is a database constraint.

## 4. The registration

One row per person per **event**, never per campaign, so somebody who received two campaigns cannot
register twice.

| Column | Notes |
| ------ | ----- |
| `event_id` | the grain. Unique with `email_normalized`, §4.1 |
| `campaign_recipient_id` | **nullable.** The recipient whose token brought them. Null on a walk-in |
| `first_name`, `last_name`, `email`, `email_normalized`, `phone` | what they typed |
| `company`, `job_title`, `country` | what they typed. `company` stays raw text |
| `reference` | `MITUR-<year>-<sequence>`, §2.3 |
| `qr_code` | 32 random bytes base64url, §2.3. Unique |
| `crm_contact_id` | nullable. Set means synced, §4.3 |
| `crm_sync_failed_at` | nullable. Set with a null contact id means pending, §4.3 |
| `cancellation_notified_at` | nullable. Set means this person was told the event is off, §4.7 |
| `contact_details_differ` | bool. What they typed disagrees with the contact on file, §4.2 |
| `details_reviewed_at`, `details_reviewed_by_id` | nullable. Stamped by `07` §6.1's acknowledge action |
| `created_at` / `updated_at` | maintained in code |

**No `status` column.** Registered is the existence of the row, and the two sync states are already
implied by the pair of nullable columns above. A third state would be invented rather than observed.
`cancellation_notified_at` does not break that rule: it records an action we took that nothing else
holds, not a state derivable from other rows (§4.7).

### 4.1 The form, and the same person twice

Seven fields: `firstName`, `lastName`, `email`, `phone`, `company`, `jobTitle`, `country`.

**The name is two fields, not one split at submit time.** "Lic. Juan Pérez de la Cruz" has no
reliable split point, and CRM's create takes `firstName` and `lastName` separately.

| Field | Phase 1 |
| ----- | ------- |
| `firstName`, `lastName`, `email`, `phone` | to CRM |
| `company` | raw text on the registration, consistent with contacts being created unattached |
| `jobTitle`, `country` | registration only. **Phase 2** they become real CRM contact fields |

**Unique on `(event_id, email_normalized)`.** A repeat submit returns `200` with the existing
registration and its codes, creates no row, and **does not overwrite what they typed the first time**.
It **does** resend the confirmation, because in practice a resubmit means "I lost the email", which is
exactly what makes the per-email rate limit load-bearing rather than defensive.

### 4.2 What reaches CRM

**Self-registration creates a CRM contact immediately**, `has_consent` true and
`consent_source = self_registration`. No human confirmation step: the person entered their own
details, which is the strongest consent record available and better than the upload path.

**Self-registration never overwrites a contact.** When the address is already in CRM, the registration
links to that contact and **CRM is left exactly as it was**.

The client's own uniqueness rule created the hazard. By decreeing one person per email,
`info@empresa.com` *is* a person, so an employee registering with the company mailbox would otherwise
overwrite whoever holds it, typically the owner. We also cannot tell a correction from a typo, and a
stranger typing "J. Perez" must not rewrite "Juan Pérez" in the system of record.

Where the details differ, the registration sets `contact_details_differ`, and `07` exposes those rows
as a list a CRM user works through by hand with the existing PATCH. No review queue, no approval
workflow, no new CRM concept. Comparison is case- and accent-insensitive over the trimmed full name.

Consequence worth stating: **when the address is taken, self-registration writes nothing to CRM at
all.**

### 4.3 When CRM is unreachable

**Depart from the `503 CRM_UNAVAILABLE` rule here, deliberately.** That rule was written for our own
staff on an admin screen, where the user retries in a minute and nothing is lost. Applied to a
stranger at an airport it means a `503` on a government portal because of a service they have never
heard of, and that person leaves and does not come back.

Persist the registration, mint the codes, send the confirmation, and mark the row as awaiting CRM
sync. Two nullable columns plus the existing flag:

- `crm_contact_id` — set means synced.
- `crm_sync_failed_at` — set with a null contact id means pending.
- `contact_details_differ` — §4.2.

**No enum**: the two states are already implied by the data, and a third would be invented. `07`
exposes the pending set as a count with a retry action, and **the retry calls the same code path the
POST calls**, so there is one contact-creation path rather than two that drift. Reconciliation is a
human action, never a background job.

A broker was rejected for this. The volume is registrations arriving during one outage. A column is
durable, queryable and visible on the screen the person is already looking at; a message in a
dead-letter queue is invisible to that person, so the broker would make the failure *harder* to see.

### 4.4 The confirmation email

Sent immediately and synchronously inside the POST, from the `registration_confirmation` template in
the event's language. It is a notification by ADR-0017's own line, one recipient with nothing tracked
per person, so convocatorias calls the send gateway for it.

Carries: event name, dates, place, the `reference` as text, the `{{qr}}` URL, and a link to the ticket
page. **Nothing about the campaign and nothing about who else was invited.**

On a §4.1 repeat submit it is resent unchanged. If the sender is unreachable the registration still
stands and the response says the email will follow, on the same reasoning as §4.3: the QR is on the
confirmation screen and the reference is printed on it.

### 4.5 The QR image

`GET /public/registrations/{qrCode}/qr`, **generated on demand**, PNG, **no storage and no named
volume**. Under a millisecond per code with `Net.Codecrete.QrCodeGenerator`; a URL payload at error
correction `Quartile` and scale 10 measured 392 bytes.

Chosen over an inline `cid:` attachment because the notification wire shape is deliberately template
plus a small per-recipient data bag, and a per-recipient binary would be an amendment to a contract
nobody has opened. `{{qr}}` goes in the data bag as a URL.

**The real cost**: mail clients that block remote images show an empty box. The confirmation screen and
the PDF badge cover that case, and the reference is printed as text beside the image.

### 4.6 A cancelled event

Settled on 2026-09-07 by the wayfinding ticket that owned it. Cancelling an event **mails every
registrant a generic notice and touches nothing else**; §4.7 is the write side.

| Surface | On a cancelled event |
| ------- | -------------------- |
| `GET /public/events` | the event is absent from the list |
| `GET /public/events/{id}` | `404`, one code shared with not-found and not-public |
| `POST /public/registrations/resolve` | `404`, one code shared with unknown, spent and revoked |
| `POST /public/registrations` | refused |
| `GET /public/registrations/{qrCode}` and its QR | **still serve.** Someone holding a ticket may still look at it |
| Existing `registration` rows | survive untouched. Nothing in any state machine deletes them. They are mailed the notice, §4.7 |
| A campaign already `sending` | can be cancelled, `03` §2. Untouched rows stay `pending` |
| **A campaign launch** | **refused.** The launch path reads the event (`04` §1 step 4) and a cancelled one blocks it, which is the only guard there is, since events-service never calls convocatorias |

A `closed` event refuses registration and token resolution too, under the same code, while its public
**detail keeps serving**. A finished event is public history and a link shared last week should still
resolve; what must not happen is somebody registering for it. Relying on the end-date check instead
would let people into an event closed early.

### 4.7 Telling the registrants

**Cancelling an event mails every registrant a generic notice.** This is routine at MITUR rather than
exceptional: two events are created for one date in different cities, a save-the-date goes out for
both, and days or months later whichever drew fewer registrations is cancelled.

**Only `cancelar` sends it.** `cerrar` sends nothing. The two terminal states are not interchangeable —
closing means the event happened, and firing the notice there would tell the attendees of every
successful roadshow that it was cancelled, days after they attended.

**It is a transactional fan-out, not a campaign.** The line: a campaign's list is *selected* by a
person from CRM, a fan-out's list is *derived* from rows we already hold. So it has no draft state, no
template choice, no launch, and no `{{enlace_baja}}` footer, which `02` forbids on a transactional
template. It never enters `07`'s `campaigns` array, so cancelling an event cannot inflate `invited`.

**No suppression check**, the one place `04` §3's refuse-to-send rule does not apply. Somebody who
opted out of marketing still has to hear that the event they registered for is off. The notice is a
duty, not an ask.

| Piece | What it is |
| ----- | ---------- |
| Template | a fifth key, `event_cancelled`, transactional, in the event's language (`02`) |
| Audience | every `registration` on the event, walk-ins and referrals included. Invitees who never registered get nothing |
| Copy | generic. **No cancellation reason** — `01` already keeps `cancellationReason` out of every public payload, and the real reason is usually "the other city sold better". Keeps the `reference` code so a confused reply is traceable |
| Record | `cancellation_notified_at`, nullable, on `registration` |
| Role | `campaigns:write`. Mailing every registrant is the same authority as launching a campaign |
| Endpoint | `POST v{version}/convocatorias/events/{eventId}/cancellation-notice`, returning `202`. One endpoint, called by the dialog and re-run from the bucket |

**`cancellation_notified_at` is not a status column.** §4's rule rejected a *state* derivable from
other rows; this records an *action we took* that nothing else holds. `stub_notification` (`04` §4.1)
is dropped by the migration that lands the real notification client, and notification-service will key
its history by event rather than by registration. The stamp is what makes the send idempotent and the
re-run safe.

**The trigger is two calls from one dialog, never automatic.** events-service never calls
convocatorias, so nothing fans out on its own without inverting the dependency this service is built
on. The frontend composes, on the convocatorias screen where the registration count is visible, both
calls carrying the human's own token: `POST /v1/events/{id}/cancel` on events-service with the reason
it already requires, then the notify endpoint here. The dialog announces the count beside the quotes
warning that is already there.

A timer polling `GET /v1/events?status=cancelled` was rejected. Cancellation is terminal, so a
fat-fingered cancel is already unrecoverable, and a poll broadcasts it to 190 people before anyone
notices.

Three guards make a missed second call visible rather than silent:

- The notify endpoint **reads the event and refuses unless it is actually `cancelled`**, so a buggy
  caller cannot mail 190 people about a live event.
- events-service unreachable is `503 EVENTS_UNAVAILABLE` and nobody is mailed, matching `04` §1.
- **A fifth reconciliation bucket** (`07` §6), service-wide: cancelled events with registrants nobody
  told, with a re-run action. It catches the failed second call, the Scalar cancel, and the browser
  closed mid-dialog.

**Cancellation is terminal**, and the cost is stated rather than hidden: `EventService.CancelAsync`
refuses from a terminal state, `UpdateAsync` refuses to edit one, and no reopen path exists, so a
mis-cancellation loses the registration set and the fix is a new event.

## 5. The registration window

Too many people have registered and MITUR wants to stop taking more. The event is still on, every
existing registration stands, and nobody is mailed anything. Settled 2026-09-07 by the wayfinding
ticket that owned it.

**This is not `cerrar`.** events-service has `EventStatus.closed`, an `events:close` role and a
`cerrar` action, all meaning the event happened and is over. The client's phrase was *"cerrar una
convocatoria"* and it means the opposite: the event is ahead of us. So the concept is a
**RegistrationWindow**, es. *inscripciones*, the column is `registrations_closed_at`, and no
identifier in this feature is called `Closed`. `GLOSSARY.md` carries the avoid list.

### 5.1 One table, and absence means open

`registration_window`, the only convocatorias table keyed by an event rather than by a campaign.

| Column | Notes |
| ------ | ----- |
| `event_id` | primary key. Same grain as `registration` |
| `registrations_closed_at` | nullable. **Null means open.** The whole state |
| `registrations_closed_by_id` | who shut it |
| `reopened_at`, `reopened_by_id` | nullable, the most recent reopen, overwritten each cycle |

**The row is inserted lazily, on the first close.** Nearly every event never has one, and the read is
a left join yielding null. Writing a row for every event convocatorias has ever seen would be the
first step toward a local copy of `Event`, and the second column somebody adds to that table is what
makes it one.

**The state is here and not on `Event` in events-service.** The tempting argument, that a field on
`Event` costs a cross-service PR, is wrong: `Event.Visibility` does not exist yet either, so both
fields would ride the same migration `01` already opens. The argument that holds is that **no
events-service code would ever branch on it**. Its list and detail filter on `visibility`, which is
why that field belongs there; nothing in events-service consults a registration window, and a field
its owner never reads is a field that rots.

**No open/close history.** The row records the last cycle. An append-only log is additive the day
somebody asks when it was first shut, and nobody has.

### 5.2 Two endpoints, not one toggle

| Route | Role | Returns |
| ----- | ---- | ------- |
| `POST v{version}/convocatorias/events/{eventId}/close-registrations` | `campaigns:write` | the window's current state |
| `POST v{version}/convocatorias/events/{eventId}/reopen-registrations` | `campaigns:write` | the window's current state |

Both are authenticated, both sit outside the public prefix, and both are idempotent in effect:
closing a closed window changes nothing and does not restamp it.

A single `PUT .../registration-window` taking `{ open: false }` was rejected. It reads as idempotent,
which invites a screen to send the current state on every save, and that stamps `reopened_by_id` with
whoever last opened the page. Two verbs also mean each audit stamp has exactly one writer.

**`campaigns:write`, not `campaigns:launch`.** The counter-argument, recorded so nobody re-derives it:
this changes what a stranger sees on a public portal, which is outward-facing the way a launch is, and
`campaigns:launch` exists separately so the client can later restrict outward acts to dirección
without a code change. It loses because what that role protects is mailing several hundred people,
which has no undo. This has an undo, it is one click, and the person who needs it is the OPT
coordinator watching the registration count.

### 5.3 What the public surfaces do

| Surface | Door shut |
| ------- | --------- |
| `GET /public/events` | **still lists the event**, with `registrationOpen: false` |
| `GET /public/events/{id}` | **still serves**, with `registrationOpen: false` |
| `POST /public/registrations/resolve` | `409 REGISTRATIONS_CLOSED` |
| `POST /public/registrations` | `409 REGISTRATIONS_CLOSED` for a new person. A repeat from an address already registered still gets its `200`, §4.1 |
| `GET /public/registrations/{qrCode}` and its QR | unaffected. A ticket already issued still serves |
| `POST /public/unsubscribe` | unaffected. Opting out has nothing to do with the door |

**The door stops new people, not somebody already in.** A repeat submit creates no row and asks CRM
nothing, so it is not a registration the window has to refuse; it is the only way a person who lost
the confirmation email gets their codes back, and every existing registration stands. The window is
read after the existing-registration lookup and after the spent-token check, so a spent private token
still gets §4.6's shared `404` rather than a `409` that would confirm it was real.

**The refusal names its reason, and §4.6's does not.** A cancelled event hides behind one `404`
covering not-found, not-public and cancelled. Here that would be wrong: the event is public, it is in
the list, the detail is serving it, and the person is holding a valid link, so a `404` would
contradict the page two clicks away. Hence a distinct code and a **`409`** rather than a `404`. The
only thing this discloses is closed versus never-existed, which is nothing on a route that only ever
serves public events.

**`registrationOpen` is a hint; the endpoint is the enforcement.** The field is what lets the portal
hide the button and print *inscripciones cerradas* on the page, instead of a person discovering it by
filling in a form. The guards stay regardless.

**The event stays in the list.** Dropping it means an event that is still happening vanishes from the
portal while its posters are up, and the OPT takes the phone calls. Listing it means people click
through and read why, which is the answer they came for.

### 5.4 Resolve refuses, and this is the part most likely to change

A private token on a closed event gets the same `409`, so the invitee lands on the closed-door page
rather than a prefilled form they cannot submit. Resolve's whole job is "here is your event, fill this
in", and succeeding would hand somebody their own name and address on a page where nothing can be done
with it.

**Flagged by the owner as the first thing to revisit.** The alternative is resolving successfully with
`registrationOpen: false` and the usual prefill, which is friendlier. It is a change to one guard and
one portal branch, so it is a decision rather than a gap.

**One consequence to accept**: a public token's referral goes unrecorded while the door is shut. Not
because resolve would have recorded it, since resolve records nothing (§2.2), but because the referral
rides on the `registration` row and no registration happens. Nobody was referred to a registration that
cannot happen.

### 5.5 A campaign launch is refused

`04` §1 step 4 already reads the event and refuses on a cancelled one. Step 5 reads the window in the
same request, before the flip to `sending`, and refuses under its own code. There is no case where mailing several hundred people a link
that answers `409` is what somebody meant, and whoever wants to invite more people reopens first.

### 5.6 What is deliberately absent

**No capacity number closes this automatically.** The client asked for a person to shut a door, not
for a limit that enforces itself, and the two are different features: a limit needs a layout chosen
from the `Venue Capacity` project's two numbers, a rule for a roadshow whose several cities use
several halls, a transaction rather than an `if` for the race at N-1, and an answer to whether a
person may reopen past the cap. Parked as a backlog card, `Investigate automatic registration closure
at a capacity number`.

## 6. Unsubscribe

**The door is convocatorias, the state is CRM.** `POST /public/unsubscribe` writes through to
`POST /v1/contacts/{id}/opt-out` on crm-service under the `contacts:write` this service already holds.

A public route on crm-service was rejected: it reopens the exact argument that kept convocatorias
standalone, an anonymous internet-facing surface bolted onto the service that holds the contact
database.

**When CRM is unreachable, the click still succeeds from the person's side.** Mirroring §4.3 so there
is one reconciliation pattern rather than two. Two nullable columns on `campaign_recipient`:

- `unsubscribed_at` — the click.
- `unsubscribe_synced_at` — set means CRM has it.

Pending is derivable from the pair. No enum, no third state. **The retry calls the same code path the
public POST calls.** And because a pending unsubscribe is not harmless, `04` §3.1 flushes every
unsynced one before any campaign's mailability check.

**Nothing clears an opt-out in phase 1.** A staff member accidentally un-opting-out a contact is
precisely the compliance failure the column exists to prevent, and there is no consent history to
record that it happened. The person's own route back is registering for an event, which does **not**
clear the opt-out but does get them their QR.

**A suppressed person may still register, and still gets their confirmation.** Suppression governs
what we send unprompted, not whether someone may reach us. Refusing a person actively trying to attend
is absurd, and withholding the confirmation leaves them with no QR.

## 7. Change requests for Roberto's portal

The portal UI belongs to a frontend project. These are what the contract above requires of it, and
they are the card that lags.

1. **Delete the whitelist path.** `registrationType` and `MOCK_WHITELIST` go. The token is the only
   credential. That mechanism admits people we never invited and carries no attribution when shared;
   `registrationType` never existed on the API.
2. **Split the name field** into `nombre` and `apellido`.
3. **Read the token from `location.hash`**, not from a query parameter, and post it in the body.
4. **Stop minting `MITUR-######` client-side.** The format is now `MITUR-<year>-<sequence>`, and
   `reference` and `qrCode` both come back from the `201`.
5. **Point the QR at our endpoint**, not `api.qrserver.com`. The PDF badge stays client-side.
6. **Handle `429`** with its `Retry-After` and machine code.
7. Keep the `es`/`en`/`fr` switch driven by `evento.language`.
8. Drop `invitedCount` and `mapEmbedUrl` from the public pages (`01` §5.1).
9. **Render the closed door** (§5). Read `registrationOpen` on the list and the detail: hide the
   register button and print *inscripciones cerradas* on the page. Treat a `409 REGISTRATIONS_CLOSED`
   from resolve or submit as that same state, not as a failure, since a person can sit on the page
   while somebody shuts the window.

## Definition of done

- A private token resolves once, prefills, and is refused on a second registration.
- A public token resolves many times, prefills **nothing**, and each registration records the
  referring recipient.
- Registering with no token succeeds on a public event and is refused on a private one.
- A repeat submit returns `200` with the original registration, creates no row, does not overwrite the
  first submission, and resends the confirmation.
- The `registration` table holds no plaintext token; a database dump yields no working link.
- No token ever appears in a log line. Grep the test run's output to prove it.
- With the CRM stub down, a registration still returns `201` with its codes, and the row is marked
  pending sync.
- With the notification stub down, the registration still returns `201`.
- The QR endpoint returns a scannable PNG generated on demand, and no file is written anywhere.
- Unsubscribe confirms with a masked address, writes through to CRM, and is idempotent on a second
  click.
- With CRM down, unsubscribe still succeeds and the row is marked pending sync.
- Every rate-limit axis returns `429` with `Retry-After` and a machine code, measured through two
  proxy hops.
- On a cancelled event: the public list omits it, detail and resolve both `404`, a new registration is
  refused, and an existing registration's ticket and QR **still serve**.
- On a `closed` event: the public detail still serves, while resolve and a new registration are refused
  under the same code. Prove it with an event closed **before** its end date, so the date check cannot
  be what refused.
- `cancellation-notice` on a cancelled event mails every registration once, stamps
  `cancellation_notified_at`, and stamps nothing on a second call. On an event that is not cancelled it
  is refused and mails nobody. With events-service down it is `503` and mails nobody.
- The `event_cancelled` body carries no `{{enlace_baja}}` and no cancellation reason. Assert both over
  the seeded row.
- Closing registrations on an event with no `registration_window` row creates one, and closing an
  already-closed window restamps nothing.
- With the window shut: the public list still contains the event with `registrationOpen: false`, the
  detail still serves with the same field, and resolve and a new registration both return `409
  REGISTRATIONS_CLOSED`. An existing registration's ticket and QR still serve, and unsubscribe still
  works.
- Reopening nulls `registrations_closed_at`, keeps the row, stamps `reopened_at`, and a registration
  that was refused a moment ago now succeeds.
- A campaign launch against an event with the window shut is refused and hands off nobody.
- The refusal code is distinct from the cancelled-event code. Assert the two are not equal, so nobody
  collapses them later.
