# 04 — The SendGrid event webhook

Inbound delivery feedback. **This is the platform's only anonymous inbound endpoint from a third
party, and it is flagged for security review.**

> **The review surface is exactly five things:** the raw-body read, signature verification **before**
> parsing, the 10-minute replay window, the 2 MB body cap, and the absence of any Keycloak policy.

**Why it is not optional.** SMTP is a one-way door: `250 Ok: queued as <token>` and the conversation
ends. Everything after — delivered, deferred, bounced, spam-reported, unsubscribed — happens later,
at SendGrid, and the webhook is the only road back. Without it the history **lies**: a suppression
drop is silent, still consumes a credit, and the row reads `sent` forever. The client has also stated
they want recipients to be able to unsubscribe *and* to know when they do, and nothing else carries
that fact to us.

**The Email Activity API is ruled out as a fallback, not merely deprioritised** — add-on gated, 6
requests per minute, 3-to-7-day retention, unavailable to EU subusers. It cannot be the history and
cannot substitute for this.

## 1. Events subscribed, and two settings in the SendGrid UI

`processed`, `dropped`, `delivered`, `deferred`, `bounce`, `spamreport`, `unsubscribe`.

> **Normative, and they are settings rather than preferences: subscription tracking ON, click
> tracking OFF, open tracking OFF.** Subscription tracking is what inserts the unsubscribe link —
> without it the `unsubscribe` event cannot exist. Click tracking rewrites every link in the body,
> which would route convocatorias' live registration token through `sendgrid.net` and into
> SendGrid's logs. Open data is unreliable under Apple Mail Privacy Protection.

> **Normative: the handler stores event types it does not recognise, and ignores unknown types when
> deriving status.** SendGrid adds event types on its own schedule, so this is needed anyway — and it
> is what keeps open/click a later UI toggle instead of a migration. There is no backfill: events
> exist only from the moment a toggle flips.

## 2. Verification — the order is the whole spec

Authentication is **the signed event webhook (ECDSA) alone**. SendGrid signs; we hold their
**public** key in `SendGrid__EventWebhook__PublicKey`. OAuth 2.0 was rejected on cost, not security:
it would mean exposing Keycloak's token endpoint to SendGrid and minting a client for them, for no
assurance the signature does not already give.

> **Normative: read the raw body, verify `timestamp + rawBody`, and only then parse.** Any
> re-serialization — model binding, a JSON round-trip, whitespace normalisation — breaks
> verification **silently**. `SendGrid.Helpers.EventWebhook.RequestValidator` takes the payload as a
> string and concatenates.

> **Normative: reject a signature timestamp older than 10 minutes.** SendGrid publishes no staleness
> tolerance. **This number is ours and must be written as ours**, so the next reader does not go
> looking for a vendor doc that says it.

> **Normative: cap the body at 2 MB and reject with `413` before reading it into memory.** SendGrid
> batches at 768 KB.

**A bad signature returns `403`, not a quiet `200`.** The failure that will actually occur is our own
public key configured wrong, and a quiet `200` bins every event permanently. With a `403`,
SendGrid's rolling 24-hour retry becomes a free grace period — fix the key within a day and the
backlog arrives on its own. A genuine forger is not retrying either way.

## 3. Exposure: a new host plus a path rule, not a public service

`notification-service` owns the endpoint. Traefik publishes exactly one route:

```yaml
- "traefik.http.routers.pgti360-hooks.rule=Host(`hooks.${DOMAIN}`) && PathPrefix(`/hooks/sendgrid/events`)"
```

**The send API stays internal-only, and the history read API is reachable only through the app
host.** The browser reads `GET /v1/notifications` and `GET /v1/notifications/batches/{id}` through
the frontend's nginx, GET only, and nothing else of the service; see ADR-0023.

> **Amended 2026-09-23 by ADR-0023.** This section first said the history read API stayed
> internal-only too. A screen has to read it from somewhere, and a BFF in events-service would
> protect nothing a token does not already protect. What follows still holds: no public host, and
> the webhook off its own host rather than `app.${DOMAIN}`.

Giving the service a whole public host was rejected because it would put **every email MITUR has
ever sent** on the internet behind nothing but a Keycloak token. Hanging the path off
`app.${DOMAIN}` was rejected because it saves one line of YAML and welds a third-party inbound
surface onto the host humans log in to.

Stage's DNS is a wildcard and Traefik already terminates TLS with a valid certificate, so the new
hostname needs **nothing from anyone** there. Production needs a DNS record, and that rides along
with the outbound-SMTP ask to MITUR's IT team.

## 4. Correlating an event to a row — a three-step ladder

**Two documented holes guarantee events that name nobody**, and both land on the event the client
cares most about:

- `unique_args` **do not attach to bounce events with a `Return-Path`**.
- `sg_message_id` is **absent on delayed or asynchronous bounces**.

| Step | Match on | When |
| --- | --- | --- |
| 1 | our opaque GUID in `unique_args` | normally |
| 2 | `email` | **only when exactly one send to that address is still open** |
| 3 | nothing — store as an **orphan** | otherwise |

> **Normative: never guess, never drop.** An orphan is a `notification_event` row with a null
> `recipient_id`: recorded, visible, unlinked, and linkable later by filling the column in.

The orphan bucket solves a second problem for free. **Stage and production share one SendGrid
account**, so an event for a GUID the receiving environment never issued simply becomes an orphan —
no corruption. Per-environment subusers were rejected as Pro-plan-only, a whole subuser hierarchy
bought for a staging convenience.

## 5. Dedup and ordering

**An append-only event log, with status derived rather than overwritten.** The webhook is
at-least-once and unordered: batching plus an independent rolling-24-hour retry per event makes
reordering structurally certain, even though no doc states it.

- A **unique index on `sg_event_id`** makes a replay a no-op insert.
- Where `sg_event_id` is absent — the docs hedge, calling it unique *"for every event where it is
  included"* — dedupe on a hash of `(event, email, timestamp, sg_message_id)` rather than dropping
  the event.

## 6. The row carries two fields, not one

> **Normative: a spam report means the mail *was* delivered.** Delivery outcome and recipient
> reaction are different facts and must never share a column. An earlier draft put `spamreport`
> above `delivered` in a single precedence ladder; that is a modelling error, recorded here so it is
> not re-made.

| Field | Values | Written by |
| --- | --- | --- |
| `delivery_status` | `queued` → `sent` → `delivered` / `bounced` / `dropped` / `deferred` / `failed` | us, then the webhook |
| `recipient_action` | `none` / `unsubscribed` / `complained` (later `opened`, `clicked`) | webhook only |

`failed` is our SMTP transaction dying — it never reached SendGrid. `sent` is the `250 Ok: queued`.

**Precedence, inside `delivery_status` only:** `bounced`/`dropped` > `delivered` > `deferred` >
`sent` > `queued`.

> **Normative: a late `processed` can never demote a `delivered`.** Reordering is certain, so this is
> the ordinary case, not an edge one.

## 7. Unsubscribes are recorded *and* enforced

An `unsubscribe` or `spamreport` writes the address into notification-service's own suppression list
(`03` §7), which refuses the next send before opening the socket.

## 8. Return 2xx inline; do not build a queue

Insert the events, recompute the affected rows, commit, return `200` — one transaction. Justified by
volume (MITUR is not a bulk sender) and by dedup: a retry after a timeout is a harmless no-op, so a
slow response costs nothing. Revisit only if a batch ever measures slow.

## 9. Local development

**Our own dev ECDSA keypair plus a script that POSTs a correctly signed fake batch at `localhost`.**
It exercises the real raw-body-and-verify path, offline, in CI, with no third party.

> **Normative: the dev keypair is generated, never committed.** Tests generate one in memory, so they
> are self-contained and deterministic and CI needs no fixture. The manual script generates one on
> first run into a **gitignored** file and prints the public key to paste into `.env`. A committed
> dev key is indistinguishable at a glance from a real one, and the next person to find it has to
> prove it is worthless before they can stop worrying.

A tunnel (ngrok / cloudflared) stays in this spec as a **one-off**, for confirming real SendGrid
traffic against stage exactly once.

**See `03` §8: pointing the fake-batch script at stage is forbidden.**

## Definition of done

- A batch signed with the configured key returns `200`, inserts one `notification_event` per entry,
  and updates the matching `notification_recipient` rows.
- **Signature verification runs against a byte-for-byte captured fixture, never a re-serialized
  object.** Prove the test would catch a re-serialization by adding a whitespace change and watching
  it fail.
- A bad signature returns `403`; a timestamp 11 minutes old returns `403`; a 3 MB body returns `413`
  without being read into memory.
- Re-POSTing an identical batch changes nothing (unique index on `sg_event_id`), and a batch with no
  `sg_event_id` dedupes on the composite hash.
- A `delivered` followed by a late `processed` leaves the row `delivered`.
- A `spamreport` sets `recipient_action = complained` and **leaves `delivery_status = delivered`**.
- An event carrying an unknown `unique_args` GUID becomes an orphan with a null `recipient_id`, and
  is visible on `/orphan-events`.
- An event with no correlation id whose address has exactly one open send links by email; with two
  open sends it becomes an orphan.
- An unrecognised event type is **stored** and ignored when deriving status.
- An `unsubscribe` suppresses the address, and the next send to it produces a `dropped` history row.
- The Traefik router matches `hooks.${DOMAIN}` on the webhook path only; `/v1/notifications` is not
  reachable from the internet.
- No Keycloak policy applies to the webhook route, and a test asserts it is the **only**
  non-health anonymous route in the service.
