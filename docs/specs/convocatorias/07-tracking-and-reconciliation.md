# Tracking and Reconciliation — Spec

The client's requirement is one sentence: *"OPT and others involved in the event would want to keep
track of who they've invited, who's registered, who hasn't registered."* This is what the API has to
return for that sentence to be answerable, plus the two reconciliation duties `06` and `04` hand here.

**Every read below is local to convocatorias**, except the cancelled-events lookup of §6.5. Reading a
sent campaign never calls CRM, because names and addresses are snapshotted on `campaign_recipient`.

## 1. The endpoint set

Fourteen routes under `v{version:apiVersion}/convocatorias/`. Reads require `convocatorias:read`; the
six actions require `campaigns:write`.

| Route | Returns |
| ----- | ------- |
| `GET /events/{eventId}/tracking` | the rollup, §2. Never `404`s, §5 |
| `GET /events/{eventId}/participants` | `PagedResponse<ParticipantRow>`, §3 |
| `GET /events/{eventId}/participants.csv` | `text/csv`, §4. Same rows, same filters |
| `GET /campaigns/{campaignId}/recipients` | `PagedResponse<ParticipantRow>`, the participants list of that campaign, below |
| `GET /campaign-recipients?contactId=` | one row per contact and launched campaign that reached it, 1 to 100 contacts (PGTI-176) |
| `GET /campaigns/{campaignId}/exclusions` | `PagedResponse<ExclusionRow>`, §6.4 |
| `GET /reconciliation` | five counts, §6 |
| `GET /reconciliation/{bucket}` | `PagedResponse<T>` per bucket |
| `POST /registrations/{id}/crm-sync` | the updated registration, §6.1 |
| `POST /recipients/{id}/unsubscribe-sync` | the updated recipient, §6.2 |
| `POST /registrations/{id}/acknowledge-details` | the updated registration, §6.1 |
| `POST /events/{eventId}/cancellation-notice` | `202`, the fan-out of `06` §4.7. **The same endpoint the cancel dialog calls and the bucket re-runs**, §6.5 |
| `POST /events/{eventId}/close-registrations` | the registration window's current state, `06` §5.2 |
| `POST /events/{eventId}/reopen-registrations` | the same shape, `06` §5.2 |

**There is no separate per-campaign recipient list.** It is the participants list filtered by
`campaignId`, so there is one row shape, one sort, one CSV and one set of filters rather than two that
drift. `GET /campaigns/{campaignId}/recipients` is that same list addressed by the campaign instead of
the event, because a direct message (PGTI-174) has no event to hang it from (PGTI-178). For an event
campaign it answers exactly what `?campaignId=` answers; for a direct message there are no
registrations to merge, so every row is `invited` and unregistered. Paging only, no filters; an
unknown campaign is `404`.

## 2. The per-event rollup

```jsonc
GET /v1/convocatorias/events/{eventId}/tracking

{
  "campaigns": [
    { "id": "…", "templateKey": "registration_invite", "state": "sent",
      "sentAt": "2026-09-01T14:02:11Z", "recipientCount": 240, "exclusionCount": 13 }
  ],
  "people":        { "invited": 227, "pendingSend": 0, "failed": 2, "suppressed": 11 },
  "registrations": { "total": 190, "fromInvitation": 154, "referred": 22, "walkIn": 14,
                     "checkedIn": 171 },
  "notRegistered": 73,
  "needsAttention": 3,
  "registrationWindow": { "open": false, "closedAt": "2026-09-06T18:20:00Z", "closedById": "…" }
}
```

**The campaign array is embedded rather than fetched separately.** An event has a handful of
campaigns, not a page of them, so it needs no paging and it saves a round trip on the screen everyone
opens first.

**`invited` means handed off, once per person**, however many campaigns reached them. A failed,
suppressed or not-yet-sent recipient is not invited. This is the number the client quotes back, so
nothing else folds into it.

**`notRegistered` is invited people with no registration.** It never counts walk-ins, or it would go
negative on a well-attended public event.

**`registrationWindow` is always present, and it is not a count.** `06` §5 puts the door in
convocatorias, so this is the read that tells somebody looking at the event that it is shut, and who
shut it. An object rather than a bare boolean because the screen prints the person and the timestamp,
and the alternative is a second call for two fields. It stays **out of `needsAttention`**, which
counts things a human must go fix; a closed door is a decision somebody already made.

**Only launched campaigns count here.** Recipients materialise at *selection*, so a draft already
holds rows for people nobody has decided to mail; counting them would put a colleague's half-built list
of 240 strangers on an OPT's screen, and would sit them in `pendingSend` beside the genuinely stranded
rows of a cancelled send, which mean the opposite thing. An unlaunched campaign's recipients are
reachable through `?campaignId=`, which is the list you review before launching. Launched means `launched_at` is set, not
"any state but `draft`": a campaign cancelled straight from `draft` never went out, so it is excluded
exactly like the draft it was, while one cancelled mid-send counts. The campaigns array and the
participants merge share that one definition.

**`needsAttention` counts pending CRM syncs plus unacknowledged `detailsDiffer` rows for this event
only.** Pending unsubscribes are deliberately service-wide (§6.2); folding them in would print the
same number on every event with none of them owning it, which is how a number stops being acted on.
Stranded campaigns are per event and **do** fold in (§6.3), because a stranded campaign belongs to
exactly one event and the OPT looking at that event is the person who should see it.

## 3. The participant row

One row per **person** per event: every `campaign_recipient` in any state, plus every `registration`,
merged. `campaign_exclusion` is **not** in this list (§6.4).

```jsonc
{
  "email": "juan@empresa.com",
  "firstName": "Juan", "lastName": "Pérez",
  "company": "Empresa SRL",
  "origin": "invited",              // invited | referred | walk_in
  "sendStatus": "handed_off",       // handed_off | pending | failed | suppressed | none
  "registered": true,
  "registeredAt": "2026-09-02T10:14:00Z",
  "checkedInAt": null,              // the CheckIn, `09` §6. Null with no registration or no CheckIn
  "reference": "MITUR-2026-00427",
  "referredBy": null,               // { "name": "Ana Gómez" } on a referred row
  "campaignIds": ["…", "…"],
  "crmSyncPending": false,
  "detailsDiffer": false
}
```

### 3.1 The join key is the email, and nothing else

**Trim plus lowercase on the address.** Contact id is never the key.

CRM decreed email unique per person, so the email *is* the identity and the contact id is derived from
it. One key that always exists beats two that disagree at the edges: a registration awaiting CRM sync
has a null `crm_contact_id`, so contact id is not always there to key on.

Normalisation is the same conservative subset `05` §5 uses — trim and lowercase, no dot-stripping, no
`+tag` handling. Drift makes us under-merge, which shows up as two rows for one person, which a human
can see. Drift the other way silently merges two people.

**The consequence, stated plainly because the client will meet it.** Someone invited at
`juan@empresa.com` who registers with `juan@gmail.com` is **two rows**: one unregistered invitee and
one walk-in. That is correct under CRM's uniqueness rule, where those two addresses are two people. It
means `notRegistered` can overcount. Nobody should discover this from a spreadsheet.

### 3.2 Three axes, never collapsed

`origin`, `sendStatus` and `registered` do not derive from each other, so they stay three fields, the
same reasoning `03` uses to keep campaign state and recipient state apart. A walk-in gets
`sendStatus: "none"`, which is a fact rather than an absence.

The mock collapsed all of this into a single `ParticipantStatus`, which is how `WAITLIST` and a
scoring system got into a screen nobody asked for.

`referredBy` carries the referring invitee's **name only**, from the recipient snapshot. Attribution
is the point; the coordinator does not need the referrer's address to read the row.

A `suppressed` or `failed` recipient **does** appear as a participant row: they are a real person we
selected and materialised, and leaving them out would make the event counts disagree with the campaign
counts.

### 3.3 Sort, paging and filters

`ORDER BY lastName, firstName, email`, page cap **100**, the platform validator unchanged. The third
key guarantees a total order, so page two never repeats a row from page one and the CSV is
reproducible.

Raising the cap for this one endpoint was rejected: that is exactly the pressure §4 exists to relieve,
and relieving it twice leaves two ways to do the same thing.

| Param | Values |
| ----- | ------ |
| `origin` | `invited` / `referred` / `walk_in` |
| `sendStatus` | `handed_off` / `pending` / `failed` / `suppressed` / `none` |
| `registered` | `true` / `false` |
| `search` | substring over name and email |
| `campaignId` | narrows to one campaign's recipients, unlaunched included |

The client's three questions are three filters over one endpoint: `origin=invited` for who we invited,
`registered=true` for who registered, `origin=invited&registered=false` for who has not.

## 4. Export is a server-side CSV, and only a CSV

`GET /events/{eventId}/participants.csv`, honouring the same five filters.

The existing client-side export ships an xlsx of whatever the page fetched, capped at 100 rows, with
no warning. An OPT with 600 invitees gets a file with 100 of them and quotes it to the client.
Streaming server-side makes the row count stop mattering.

| Decision | Value |
| -------- | ----- |
| Headers | **Spanish.** An OPT staffer opening this in Excel is an end user, and the audience rule governs |
| Encoding | **UTF-8 with BOM.** Without it Excel on Windows renders `Peña` as `PeÃ±a`, and accented names are most of this file |
| Delimiter | comma, RFC 4180 quoting — **see the open item** |
| Columns | identical to the JSON row of §3, `crmSyncPending` and `detailsDiffer` included |
| Disposition | `attachment; filename="convocatoria-<evento>-<yyyy-MM-dd>.csv"` |

**Amended 2026-10-10 (PGTI-128):** the row's `checkedInAt` (`09` §6) is a column too, *Fecha de
check-in*, written like *Fecha de inscripción*: UTC, no fractions, empty with no CheckIn. It goes
**last**, not beside `registeredAt`, so the columns of spreadsheets already built on the export keep
their positions.

No xlsx and no PDF server-side. The frontend keeps its client-side buttons for anything it can already
render on screen.

**Open item, do not guess at it.** Excel honours the system list separator, which in several Spanish
locales is a semicolon, so a comma file can open in a single column on the client's machines. One test
on a real MITUR laptop settles it. It is a five-minute check and the wrong answer makes every export
look broken.

## 5. What we will not claim to know

**No `delivered`, `opened`, `clicked` or `bounced` field anywhere in these responses.** Not nullable,
not behind a flag: **absent**.

Delivery feedback waits on the notifications webhook question, which is open and blocked on the
client's provider choice. A field that is always null is an invitation for a screen to render `0`, and
for somebody to read that as an open rate of zero. Nothing can render what the response does not
carry, and adding the fields later is additive and breaks no client.

Everything above is ours: handed off, failed, suppressed, excluded, registered, referred, walk-in. Say
`entregado al proveedor`, never `entregado`.

> **Amended 2026-10-03 (PGTI-177).** The webhook question is answered, so delivery is now known for
> two reads: a campaign's own recipients (`?campaignId=` and `.../campaigns/{campaignId}/recipients`)
> and the contact read behind a company's Convocatorias tab. They carry `deliveryStatus`, one of
> `sent`, `delivered`, `bounced`, `spam_report` or `unsubscribed`, read live from notification-service
> by the message id kept at send. The rule above still holds where it was written for: the
> event-wide list, the CSV, the rollup and reconciliation carry nothing, and a row never handed off
> leaves the field out. `opened` and `clicked` stay absent everywhere.

**When the webhook question is answered, bounces will be the expensive half.** The transport is
SendGrid SMTP relay (confirmed 2026-09-12), where `custom_args` travels as `unique_args` — and
SendGrid documents a gap: *"If your email has bounces with the `Return-Path`, the `unique_args` don't
attach to the event."* **A bounce event may therefore arrive carrying no correlation id**, which means
reconciliation cannot always join it back to a recipient row the way a delivery event can. The
fallback is matching on the email address, which is weaker and ambiguous when one address appears in
two campaigns for the same event. Nothing here changes today, because none of these fields exist yet;
it is recorded so that whoever answers the webhook question does not cost it as free. Sourced in
`.scratch/notifications-map/research-sendgrid-smtp-relay.md`.

**An unknown event id returns `200` with a zeroed body, never `404`**, with `registrationWindow`
reading `open: true` and null stamps, because no row means open (`06` §5.1). §7 means convocatorias never
calls events-service on these reads, so it cannot tell a nonexistent event from a real one with no
campaigns yet. A `404` would be a lie roughly half the times it fired: an event created this morning is
not a missing event. Convocatorias has no authority over what events exist, and the screen calling this
got its id from events-service anyway.

## 6. One reconciliation surface, five buckets

Nothing in this service retries on its own. That was decided twice, in `06` §4.3 and in `04` §5. This
is the screen that makes those stranded writes visible, and the only thing that closes the loop.

```jsonc
GET /v1/convocatorias/reconciliation

{ "pendingRegistrationSync": 3, "pendingUnsubscribes": 1, "detailsDiffer": 7, "strandedCampaigns": 0,
  "uncancelledNotices": 0 }
```

`GET /reconciliation/{bucket}` returns a standard `PagedResponse<T>` per bucket, each row carrying its
event where it has one. The buckets are `pending-registration-sync`, `pending-unsubscribes`,
`details-differ`, `stranded-campaigns` and `uncancelled-notices`, one route each, and each count is
its bucket's row count: `uncancelledNotices` counts events, not registrations. A single fat response
with three arrays was rejected: it needs a bespoke envelope, it cannot page, and it breaks the day one
bucket holds 400 rows after an outage.

### 6.1 Registrations awaiting CRM sync, and details that differ

`POST /registrations/{id}/crm-sync` calls **the same contact-creation code path the public POST
calls**, so there is one path rather than two that drift. A `409` is a match and not an error: it links
the existing contact and re-checks `canBeEmailed` on the returned body.

`contact_details_differ` is the other half of the same screen. It means the contact exists and
disagrees with what the person typed, and **there is nothing to retry** — the fix is a human editing
CRM. So it gets `POST /registrations/{id}/acknowledge-details`, stamping `details_reviewed_at` and the
user id.

Two alternatives were rejected. Clearing it automatically by re-reading CRM puts a CRM call back into a
read path deliberately kept local. Leaving it informational forever means the screen never empties,
which is precisely how a reconciliation queue trains people to ignore it.

The two share a screen because they are worked by the same person in the same sitting, and two screens
for "needs a human" is one too many.

### 6.2 Pending unsubscribes

`POST /recipients/{id}/unsubscribe-sync`, the same code path as the public unsubscribe.

**Service-wide, not per event**: a pending unsubscribe belongs to the person, not to the campaign whose
email they happened to click. The launch path already flushes every unsynced unsubscribe before the
mailability check (`04` §3.1), so this bucket is a safety net rather than the only defence.

### 6.3 Stranded campaigns

A campaign in `sending` with at least one recipient row still `pending`, and no worker holding it.
The worker is in-process and its queue is in memory (`04` §1.1, ADR-0018), so a redeploy mid-send
leaves exactly this: some rows `handed_off`, some `pending`, the campaign stuck in `sending` forever
because nothing sets `sent` until the last row goes terminal.

The bucket exists because **nobody is watching by design**. The `202` sends the launcher away on
purpose, and a person who closed their laptop at the `202` is doing the thing the ADR told them they
could do. Something has to notice on their behalf, and a bucket on a screen somebody already opens is
cheaper than a second alerting mechanism.

Its rows carry the campaign, the event, `launched_at`, and the split of recipient rows by status, so
the counts that do not add up are the evidence rather than something to go and check. Two actions,
both already specified in `03` §7 and `04` §5: `resume-handoff` for the `pending` tail, `retry-failed`
for the per-recipient rejections.

**How stranded is detected is deliberately dumb**: state is `sending`, at least one row is `pending`,
and `launched_at` is older than a threshold. One case it therefore misses, knowingly: `retry-failed`
on a `sent` campaign (`03` §7) puts rows back to `pending` without leaving `sent`, so a worker death
right after leaves `sent` plus `pending` rows, which this bucket never shows. Pressing `retry-failed`
again clears it — the route enqueues even when it reopens no row — and widening the filter to catch a
window measured in seconds would put every mid-retry campaign in the bucket. No lease table, no heartbeat, no worker registry. A
campaign whose worker is alive and mid-batch may appear here briefly, which is a false positive that
costs a person one refresh, and `resume-handoff` walking the unsent tail (`04` §5) makes acting on it
harmless anyway. **Set the threshold against our handoff, not against delivery.** notification-service
drains its own SMTP queue after answering `202` (`04` §2.1), so a campaign can be entirely
`handed_off` — and therefore not stranded — while mail is still physically going out. A threshold
tuned to when recipients receive mail would put healthy campaigns in this bucket every time. A lease would be real infrastructure bought to avoid a stale row on a screen
refreshed by hand.

### 6.4 Exclusions are a campaign question, not an event one

`GET /campaigns/{campaignId}/exclusions` — name snapshot, reason code (`no_email`, `opted_out`,
`do_not_invite`, `contact_unavailable`) and `excludedAt`. Convocatorias renders its own Spanish per
code.

This is the endpoint that answers *"I selected 240 and see 227, who is missing"*, which is why `03`
§6.1 made exclusions a table rather than a counter.

They stay out of the participants list on purpose. Those people were never materialised, the reason is
per campaign, and someone excluded from campaign one but mailed in campaign two would need a rollup
rule that means nothing.

### 6.5 Cancelled events nobody was told about

A `cancelled` event holding registrations whose `cancellation_notified_at` is null. **Service-wide**,
like pending unsubscribes, and for the same reason: the duty belongs to the people, not to whichever
screen someone happens to open.

The bucket exists because the notice is fired by the frontend as a second call after the event cancel
(`06` §4.7), which is the one arrangement that keeps events-service from having to call us. A second
call is a call that can fail: the browser closed, the network dropped, or somebody cancelled the event
through Scalar on stage and never touched convocatorias at all. Nothing else would ever notice.

Its rows carry the event, `cancelledAt`, and how many registrations are still unnotified. This is the
one bucket that is not local: which events are cancelled is read from `GET /v1/events?status=cancelled`
when the screen is opened, and `cancelledAt` is that event's `updatedAt`, since a cancelled event
accepts no further edit. With events-service down the summary reports `uncancelledNotices: null` and
the bucket answers `503 EVENTS_UNAVAILABLE`; the other four counts are unaffected. One action,
a re-run of the fan-out, which is safe to press twice because `cancellation_notified_at` is stamped per
row.

## 7. events-service consumes none of this

**One exception, added by `09` §7:** the raffle reads the Event's check-in list through its own
route, under `check-in:service-read`. It is a raffle reading who came through the door, not an Event
read waiting on this service, so everything below still holds.

`Event.InvitedCount` stays the OPT's manually-typed target; the actuals live here. The frontend calls
both services and shows one beside the other.

The alternative, events-service calling convocatorias for a live count, creates a second
service-to-service edge running the opposite way to the only one that exists, and makes an event read
fail when convocatorias is down, so that a page can print two numbers side by side.

For the same reason **the tracking reads do not embed the event's name or dates**. That would make the
tracking screen fail when events-service is down, in exchange for a header the SPA already has. The
portal republishes event fields because a stranger cannot call events-service at all; a logged-in
coordinator can.

## Definition of done

- One person invited by two campaigns is **one** row, and `invited` counts them once.
- An unlaunched campaign's recipients (a draft, or one cancelled from draft) appear under
  `?campaignId=` and in **no** rollup count.
- A walk-in has `origin: walk_in`, `sendStatus: none`, and never appears in `notRegistered`.
- A registration through a forwarded public token has `origin: referred` and `referredBy` naming the
  original invitee.
- Someone invited at one address who registers at another is two rows, and the test says so explicitly
  so nobody later reads it as a bug.
- The three client questions are answerable as three filter combinations, asserted as three tests.
- The CSV streams past 100 rows, opens in Excel with accents intact, and carries the same rows as the
  JSON under the same filters.
- An unknown event id returns `200` with zeros and `registrationWindow.open: true`.
- The rollup reports `registrationWindow.open: false` with the closer and the timestamp after
  `close-registrations`, and back to `true` after `reopen-registrations`, and neither moves
  `needsAttention`.
- No response body anywhere contains the strings `delivered`, `opened`, `clicked` or `bounced`.
  Assert it once over the serialized shapes.
- `crm-sync` on a pending registration creates the contact through the same path the public POST uses,
  and a `409` links instead of failing.
- `acknowledge-details` clears the row from the bucket and the count drops.
- A campaign left `sending` with `pending` rows and a stale `launched_at` appears in
  `strandedCampaigns`, and `resume-handoff` clears it without re-mailing a `handed_off` row.
- A cancelled event with unnotified registrations appears in `uncancelledNotices`, and the count drops
  to zero once the fan-out runs.
- `cancellation-notice` on an event that is **not** `cancelled` is refused and mails nobody. Called
  twice on a cancelled one, it mails each registrant exactly once.
- A caller without `convocatorias:read` gets `403` on every read; a caller with it but without
  `campaigns:write` gets `403` on all six actions.
