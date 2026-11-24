# convocatorias-service: the event and the registration portal

> Moved from `convocatorias-service/CLAUDE.md` (PGTI-212), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' convocatorias-service/docs/*.md` finds it.
> Spec numbers (`03`, `04` §1, ...) are files in `docs/specs/convocatorias/`.

## The event, and the public cartelera

`docs/specs/convocatorias/01-events-contract.md` is the decision; what follows is where it lives.

- `Core/Events/` — `IEventDirectory`, `EventSummary`, `EventCity`, `EventPlaceholderValues`, plus
  `EventStatus` and `EventVisibility`, which are **copies of events-service's closed sets**, not
  authorities. No transport appears in any signature here, which is what lets domain tests
  substitute the directory. `EventPlaceholderValues` is where `{{evento}}`, `{{lugar}}` and
  `{{fecha}}` come from, and **a value it cannot fill comes back empty, never as plausible filler**:
  empty is the only thing the launch knows how to refuse.
- `Infrastructure/Events/` — `EventsClient` (the only place an events-service route appears), its
  wire DTOs, and `EventDirectory`, which translates a transport failure into
  `503 EVENTS_UNAVAILABLE`. Wired with the chassis `AddServiceClient` on `Services__Events__BaseUrl`,
  authenticated by the `audiencia-events-api` mapper on this service's realm client. The cartelera's
  paging is **not** here: it comes from `Infrastructure/Neighbours/NeighbourPageReader`, shared with
  the CRM directory (see "The campaign model").
- `Web/PublicEndpoints/` — `GET v{version}/public/events` and `/{id}`, anonymous, serving
  `PublicEventResponse` / `PublicEventDetailResponse`.

Five rules worth knowing before changing any of it:

- **There is no degradation ladder and no cache**, unlike the OPT directory in events-service. A
  cached public list is a cached *visibility decision*, which is how a private event stays visible
  for five minutes after somebody hides it. events-service unreachable is a hard
  `503 EVENTS_UNAVAILABLE` on all three reads — the launch and both public ones — and nobody is
  mailed with a hole where the event name goes.
- **The public routes are a narrowed republish, never a proxy.** events-service is not reachable
  from the internet, so it keeps its fallback policy with no exception to audit, and this service
  re-emits a handful of fields. `estimatedBudget`, `totalCost`, `invitedCount`, `optContact`,
  `cancellationReason` and every audit field come over the wire and are dropped here; they cannot
  leak by accident because `EventSummary` has nowhere to put them.
- **One `404` code for four reasons.** Unknown id, private, cancelled and malformed all answer
  `PUBLIC_EVENT_NOT_FOUND`. Telling a stranger which one it is confirms the event exists and that
  MITUR hid it. A `closed` event, on the other hand, **still serves its detail**: a link shared
  last week has to keep resolving.
- **The visibility and status filter is applied twice**, once by events-service's own query and
  again in `EventDirectory` before publishing. The second condition costs nothing and is what
  stops a semantic change on the other side from exposing a private event. It runs **after** the
  paging, not inside it: what decides whether a page was full is what events-service sent, so a
  filtered-out event can never be mistaken for the end of the listing.
- **`registrationOpen` is the one field not from events-service.** It is this service's own
  registration window (`06` §5), left-joined onto each event: one `ListShutAsync` read per list
  page, one `GetAsync` on the detail. It is here rather than on `Event` because no events-service
  code would ever branch on it. The registration routes reuse `PublicEventDetailResponse` but leave
  it null, and a null is not serialized.

Integration tests drive the whole chain against a WireMock `EventsStub` rather than substituting
`IEventDirectory`, because the recut is precisely what needs exercising and a double that already
returns the narrow type cannot fail at it. The stub therefore serves the **whole** events-service
response, budget and invited count included: what proves the portal does not republish them is
that they were there to republish. A class that takes events-service down gets its own factory —
the chassis circuit breaker stays open for fifteen seconds and would otherwise fail the next class
for an outage that is not its own.

**The launch path reads the event as step 4 of its order, and that read is the only guard on
the event there is.** events-service never calls convocatorias, so nothing else in this service can
learn the event was cancelled an hour ago. Three things come out of it: events-service unreachable
is `503 EVENTS_UNAVAILABLE`, a cancelled event is `409 CAMPAIGN_EVENT_CANCELLED`, and an event with
no value for `{{evento}}`, `{{lugar}}` or `{{fecha}}` is `400 TEMPLATE_EVENT_VALUES_MISSING` naming
the fields. An id events-service answers `404` for falls into that last one naming all three: no
event, no values.

Two things about it are worth knowing before changing it:

- **It runs after the two CRM steps, not before**, which is where `04` §1 puts it. That order is
  pinned by a test that compares arrival times across the CRM and events stubs, and by a second one
  that does not need a clock: with events-service down, both CRM calls already happened.
- **All three event-level placeholders are checked, not only the ones the campaign's template
  names.** Reading the template here would mean picking it by the event's language and refusing the
  variant that does not exist (`02` §3), which is still to come; both seeded campaign templates name
  all three, so today it is the same answer. `TemplateBodyRules.CheckEventValues` is the seam that
  work uses. The values themselves come from `Core/Events/EventPlaceholderValues`, and the launch
  copies them onto the campaign row as `CampaignEventValues`, which the worker turns back into
  `sharedData`.

**A campaign that convokes an event events-service does not know cannot be launched.** Integration
tests that need a `sending` campaign therefore create it against `EventsStub.EventId`, not a fresh
`Guid`.

## The registration portal

A stranger clicks a link, the page resolves their token, they post the form, and they get back a
reference and a QR code. Nobody logs in (`docs/specs/convocatorias/06-registration-portal.md`).
One table, `registration`, and two anonymous routes under the public prefix:
`POST /public/registrations/resolve` and `POST /public/registrations`.

Seven things are worth knowing before touching it:

- **Both routes are `POST` and the token travels in the body, never in a URL.** The email links to
  `#t=<token>`, and a browser does not send a fragment to the server, so the token reaches no access
  log. Serilog drops the query but nginx and Traefik record the whole request line, on disk, in
  every environment. That is the entire reason resolve is not a `GET`, and
  `Resolve_Returns405_WhenCalledOverGet` is what stops somebody adding one.
- **One token, two policies, decided by the event's visibility** (`06` §2.2). A private event's
  token admits exactly one registration and prefills the invitee's three fields; a public event's
  token admits many, prefills **nothing**, and each registration records the referring recipient.
  Prefilling on a public token would show whoever received a forwarded link the original invitee's
  name and address.
- **The token is optional on submit and there is one endpoint, not two** (`06` §1.1). With no token
  a registration succeeds on a public event and is refused on a private one, leaving
  `campaign_recipient_id` null. Two routes would duplicate every validation and every rate limit for
  one nullable column.
- **Every refusal is the same `404 PUBLIC_REGISTRATION_NOT_FOUND`** — unknown, spent or revoked
  token, and an event that is unknown, private, cancelled, closed or past its end date. It is a
  *different* code from `PUBLIC_EVENT_NOT_FOUND`, which the public detail keeps, because a closed
  event still serves its detail while refusing registration. Separating the reasons would tell
  somebody probing addresses which one is really invited. **The one exception is a shut
  registration window**, which is `409 REGISTRATIONS_CLOSED` — see "The registration window".
- **A repeat submit is a `200`, not a `409`.** It returns the existing registration with its same
  two codes, creates no row, and does not overwrite what the person typed the first time. The
  uniqueness rule is a **database constraint** on `(event_id, email_normalized)` and not an
  in-memory check, because two simultaneous submits both pass any `SELECT` that runs first;
  `RegistrationRepository` catches that one constraint by name and reads back the winner.
- **The spent-token check runs *after* that read, and the order is the feature.** A private token
  is spent by its own first registration, so checking it first would `404` the one person §4.1's
  `200` exists for: the invitee who lost her email and resubmits. Somebody arriving with a spent
  token and an address that is *not* registered is a different person, and that is what the check
  still refuses. `Register_Returns200WithTheFirstRegistration_WhenAPrivateInviteeRegistersTwice`
  pins it. The accepted consequence, stated because `06` §4.1 chose it: anyone who can submit a
  valid form reads back an existing attendee's `qrCode`, and on a public event that needs no token
  either, only the victim's address. **The per-email rate limit is not what holds that shut** — it
  catches mail-bombing by resubmission (`06` §3) and a read-back needs one request, so 5/min never
  binds. What the spec offers is `06` §2.3: presenting a QR must not be the only proof of identity
  at the door. Worth re-reading when PGTI-58 makes the code fetchable.
- **Two codes with different jobs** (`06` §2.3). `reference` is `MITUR-<year>-<sequence>`, short,
  printed, and carries no authority; `qr_code` is 32 random bytes base64url, unique, never shown as
  text. The sequence is **one PostgreSQL sequence and not one per year**: it never hands the same
  number twice under concurrency, and restarting it each January would need either DDL at runtime or
  a counter row serialising every registration.
- **The per-token and per-email rate-limit axes are real now.** `UsePublicRateLimitPartitions` runs
  **before** `UseRateLimiter` and lifts the two partition keys off the JSON body, because the
  limiter's partitioner is synchronous and runs before any endpoint exists. Setting them from a
  handler would arrive one request too late. Without that middleware, attaching the per-email policy
  would just be a second, much narrower per-IP limit, and one office registering from a single
  uplink would bounce at the sixth person.

**Registering creates the CRM contact, and the CRM can never refuse a registration** (`06` §4.2,
§4.3). `RegistrationService.SyncWithCrmAsync` is what talks to CRM; the last two rules below are
about *when* it is called and live in `RegisterAsync` around it:

- **Create, never overwrite.** A free address is created with `has_consent` true and
  `consent_source = self_registration` — the strongest consent record there is, which is why this
  path has no human confirmation step while the upload path does. A **taken** address is linked and
  CRM is left exactly as it was, so a taken address means self-registration writes nothing to CRM at
  all. The client's own one-person-per-email rule is what makes that necessary: `info@empresa.com`
  *is* a person, so an employee registering with the company mailbox would otherwise overwrite
  whoever holds it, and a stranger typing "J. Perez" must not rewrite "Juan Pérez" in the system of
  record.
- **A `409` is the taken-address path, not a second one.** `ICrmContactDirectory` returns the live
  contact inside the creation result, so the race between the lookup and the create lands on the
  same branch as the lookup that found somebody. Two branches would drift.
- **`contact_details_differ` is what a mismatch does instead**, and `07` exposes those rows as a
  list a CRM user works through by hand with the existing `PATCH`. No review queue and no new CRM
  concept. The comparison is `Core/Shared/PersonNameShape`: case- and accent-insensitive over the
  trimmed full name, and **nothing more** — a stranger on a phone types no accents, so flagging that
  would fill the review list with noise until nobody read it.
- **CRM unreachable is the one place the `503 CRM_UNAVAILABLE` rule is deliberately broken.** That
  rule was written for our own staff on an admin screen; applied to a stranger at an airport it is a
  government portal down because of a service they have never heard of. The registration persists,
  the codes are minted, and the row is marked awaiting sync. Two nullable columns say which:
  `crm_contact_id` set means synced, `crm_sync_failed_at` set with a null contact id means pending.
  **No enum** — the two states are already implied by the data. `07` exposes the pending set with a
  retry that calls this same code path, so there is one contact-creation path rather than two.
  Reconciliation is a human action, never a background job.
- **The phone has the CRM's shape** (PGTI-355). crm-service refuses a malformed contact phone with
  a 400, and `CrmContactsClient` reads any non-success as the CRM being down, so a phone it would
  refuse would leave the registration pending through every retry. The portal checks it with
  `Core/Shared/PhoneShape`, a copy of the CRM's `PhoneRules`, and answers a field 400 on `Phone`;
  `CrmContactDirectory` also sends no phone that breaks it, which covers rows stored earlier.
- **CRM is called before the row is written, not after.** §4.3 reads like an ordering but is about
  the response. Writing first and updating second would leave, on a crash in between, a row with
  neither column set — a third state the model has no name for and `07`'s bucket cannot see. Called
  first, every row is born either synced or pending, in one write.
- **A repeat submit asks CRM nothing.** The sync state belongs to the row that already exists, and
  re-syncing on every resend would let a later outage turn a synced registration into a pending one.

The pending-sync bucket and the review action live on the reconciliation screen, see "The
reconciliation screen" below.

## The registration window

MITUR has enough people and wants to stop taking more; the event is still on and nobody is mailed
(`06` §5). One table, `registration_window`, keyed by `event_id` — the only table here keyed by an
event rather than a campaign — and two routes, `POST v{version}/convocatorias/events/{eventId}/
close-registrations` and `.../reopen-registrations`, both `campaigns:write`.

- **Not `cerrar`.** `EventStatus.closed` means the event already happened. No identifier here is a
  bare `Closed` or `Close` (`GLOSSARY.md` carries the avoid list).
- **Absence means open, and the row is written lazily on the first close.** Reopen never inserts
  one. `RegistrationWindow.Open(eventId)` is what every read returns for a missing row.
- **Idempotent in effect, decided inside the statement.** Close is an `INSERT ... ON CONFLICT DO
  UPDATE ... WHERE registrations_closed_at IS NULL`, reopen an `ExecuteUpdate` filtered on a closed
  row, so two simultaneous closes cannot restamp each other. Reopen keeps
  `registrations_closed_by_id` as the last closer; no open/close history.
- **Public surfaces:** list and detail still serve the event with `registrationOpen: false`;
  resolve and submit answer `409 REGISTRATIONS_CLOSED`, a private token included (`06` §5.4); the
  ticket, its QR and unsubscribe are untouched. The window is read only **after** the event read and
  the spent-token check, so a cancelled event and a spent private token still hide behind the
  shared `404`: a `409` there would confirm the token was real.
- **A repeat submit still gets its `200`.** The window check in `RegisterAsync` runs after the
  existing-registration read: the door stops new people, not somebody who lost their email.
- **Launch step 5** reads the window after the event and before the flip, and refuses with
  `409 CAMPAIGN_REGISTRATIONS_CLOSED`, deliberately distinct from `CAMPAIGN_EVENT_CANCELLED`
  (a unit test asserts both codes differ from the cancelled ones).

## The confirmation email

A successful registration mails the person their codes (`docs/specs/convocatorias/06-registration-portal.md`
§4.4). It is a **notification, not a campaign** (ADR-0017): one recipient, nothing tracked per
person, so it goes out through its own transport seam rather than the campaign send path.

- **`RegistrationConfirmationSender` (Core) arms it, `ITransactionalNotifier` (Core) sends it.** The
  sender reads the `registration_confirmation` template in the event's language, builds the shared
  bag (`evento`/`lugar`/`fecha`, from `EventPlaceholderValues`) and the recipient bag (`nombre`, the
  `referencia` as text, the `qr` image URL and the `enlace_entrada` ticket link), and hands a
  `TransactionalNotification` to the notifier. It does **not** substitute — that is the receiver's
  job, exactly as on the campaign path (`04` §2).
- **Two transports behind the same `Notifications__Transport` switch as campaigns.**
  `StubTransactionalNotifier` writes one `stub_notification` row; `HttpTransactionalNotifier` posts
  the one-recipient batch to `POST /v1/notifications` with `about` = the event only (no campaign).
  Both are wired in `AddCampaignSendTransport`.
- **The send runs from the endpoint, not inside `RegisterAsync`**, so the delicate register flow
  stays untouched and both the new (`201`) and the repeat (`200`, "I lost the email") paths mail
  uniformly. **Its failure never touches the response**: the row is already written, the QR is on the
  confirmation screen and the reference is printed, so a failed or even throwing send is logged and
  swallowed (`06` §4.4). A **suppressed** person still gets it: suppression governs what we send
  unprompted, not the answer to a person's own act (`06` §6).
- **The confirmation copy gained the reference and the ticket link.** That is a copy change to a
  frozen seeded template, so it shipped the sanctioned way: `TemplateSeed` edited, its fingerprint
  updated, and a data migration (`UpdateRegistrationConfirmationBody`) that brings existing databases
  to the new body. Two placeholders were added to `SupportedPlaceholder`, both **recipient** grain:
  `referencia` and `enlace_entrada`.

## The cancellation notice

Cancelling an event mails every registrant a generic notice (`06` §4.7).
`POST v{version}/convocatorias/events/{eventId}/cancellation-notice`, `campaigns:write`, `202`
with `{ eventId, notified, failed }`. `CancellationNoticeService` (Core) owns it and sends through
the same `ITransactionalNotifier` as the confirmation.

- **A fan-out, not a campaign.** The list is every `registration` on the event, walk-ins and
  referrals alike; no draft, no launch, no `{{enlace_baja}}`, and nothing in `07`'s `campaigns`.
- **It reads the event and refuses unless it is `cancelled`**, `409 EVENT_NOT_CANCELLED`. A `closed`
  event is refused too: closing means it happened. An unknown id gets the same code. events-service
  down is `503 EVENTS_UNAVAILABLE`. Both refusals mail nobody.
- **No suppression check**, the one exception to `04` §3: the notice is a duty. This path asks CRM
  nothing.
- **Claim, send, release.** Each row is stamped with a conditional `UPDATE ... WHERE
  cancellation_notified_at IS NULL` *before* its send, so two presses at once cannot mail anybody
  twice. A failed or throwing send nulls the stamp again, which keeps the row in `07` §6.5's bucket.
  Accepted: a crash between a claim and its send leaves that one row stamped and unmailed.
- **The endpoint takes no `CancellationToken`**, for the same reason as the handoff worker: a closed
  tab must not stop the notice halfway.
- **The QR is untouched.** Nothing is revoked or stamped on it; the ticket and its QR keep serving.
- The `event_cancelled` copy gained `{{referencia}}` so a confused reply is traceable. Shipped the
  frozen-copy way: `TemplateSeed` edited, fingerprint updated, and `AddCancellationNotice` carries
  both the column and the `UpdateData` of the body.
