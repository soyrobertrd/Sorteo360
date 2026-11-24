# Sending and Handoff — Spec

What happens when someone presses launch: the validation, the suppression flush, the mailability
re-check, the flip to `sending`, and then the render and the call to notification-service on a
worker. notification-service does not exist, so this spec also defines the stub that stands in for it
and the wire contract the real service will have to honour.

## 1. The launch path, in order

**The request does the deciding; a worker does the mailing.** The line between them is the flip to
`sending`, which is also where the `202` is returned. ADR-0018 is the decision and the reasoning; this
section is the order.

**Inside the request**, in this order, every step able to refuse:

1. **Validate the launch.** The campaign's template must exist in its language, or
   `400 CAMPAIGN_TEMPLATE_NOT_FOUND` (`03` §4.1). `expectedRecipientCount` must match, or `409 CAMPAIGN_RECIPIENTS_CHANGED`
   (`03` §5.1). Zero recipient rows is `400 CAMPAIGN_HAS_NO_RECIPIENTS`. All rows unmailable is
   `400 CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS` (`03` §4, and §3 below).
2. **Flush every unsynced unsubscribe to CRM** (§3.1). If the flush cannot complete, stop:
   `503 CRM_UNAVAILABLE`, nobody is mailed.
3. **Re-check mailability** for the whole recipient list (§3). If the check cannot complete, stop:
   `503 CRM_UNAVAILABLE`, nobody is mailed.
4. **Read the event** from events-service for `{{evento}}`, `{{lugar}}` and `{{fecha}}`. Unreachable
   is `503 EVENTS_UNAVAILABLE`; a missing event-level value is `400 TEMPLATE_EVENT_VALUES_MISSING`
   naming the field (`02` §2.2); **a cancelled event refuses the launch** with
   `409 CAMPAIGN_EVENT_CANCELLED`. An id events-service answers `404` for falls into the
   missing-value refusal naming all three: no event, no values. That read is the only
   guard, because events-service never calls convocatorias, so nothing else can learn the event is
   off. See `06` §4.6.
5. **Read the registration window** (`06` §5) in the same step. A shut window refuses the launch under
   its own code, distinct from the cancelled one. Nobody means to mail several hundred people a link
   that answers `409`, and whoever wants to invite more people reopens first.

   **A campaign with no event skips steps 4 and 5** (`03` §1.1): there is no event to read and no
   window to check, and steps 1 to 3 run exactly as for any campaign. Nothing is stamped in their
   place; the worker reads the subject and message off the row.
6. **Flip the campaign to `sending`**, stamp `launched_by` and `launched_at`, enqueue the campaign id,
   and return **`202 Accepted`** with `Location: /v1/convocatorias/campaigns/{id}`.

**On the worker**, with no browser attached to it:

7. **Mint the two tokens** per recipient, snapshot name and address, render the body once, and post
   the batches (§2), chunked at 1,000. A campaign with no event mints only the unsubscribe token: its
   recipients have no registration hash, so they cannot register.
8. **Map the per-recipient results** onto `send_status`, stamp `sent_at` and land on `sent` when no
   row is left `pending`.

Everything that can be refused is refused before the `202`. After it the answer is always "we are
sending", and the campaign resource is where you find out how far it got.

### 1.1 The worker

A `BackgroundService` draining a `Channel<Guid>` of campaign ids, in this process. It is the
platform's first background execution, which is why it has an ADR.

**It runs only while there are batches**, because `ReadAllAsync` blocks on an empty channel. There is
no timer, no scan and no wake-up interval. This is not the startup scan the approval-era spec
rejected: that one fired on every deploy over every campaign in the database and would cheerfully
resume a campaign whose event was cancelled an hour earlier. This one holds exactly the ids a person
launched in this process's lifetime.

**Its cancellation token is `ApplicationStopping`, never `RequestAborted`.** That is the entire point.
A `CancellationToken` bound from a Minimal API handler is `HttpContext.RequestAborted`, so passing it
down a batch loop means the loop dies when the browser tab closes, which is precisely the
close-the-laptop case (ADR-0018). No handler token reaches step 7 or step 8.

**Durability is unchanged and no better.** The channel is in memory, so a redeploy mid-send strands
the campaign in `sending` with rows on both sides. That was already true of the synchronous path and
is not what this change fixes. What closes it is a person, through `resume-handoff` (§5), and
`strandedCampaigns` in `07` §6 is what puts the campaign in front of them.

### 1.2 What the screen sees

`202` means accepted, not finished. The campaign resource carries `state` plus the recipient counts,
so a screen polls `GET /campaigns/{id}` and watches `pending` fall to zero. A user who closes the
laptop at the `202` comes back to a campaign that is `sending` or `sent`, and either way the rows say
exactly who was mailed.

**A campaign sitting in `sending` is normal, not stuck.** Our worker's `pending` tail falls as batches
are accepted, but notification-service drains its own SMTP queue behind that (§2.1), so `sending` is a
wider window than the accept latency alone suggests. Screen copy must not imply a stall, and `07` §6.3
sets its stranded threshold with this in mind.

## 2. The notification contract

`POST /v1/notifications` on notification-service. This **amends a closed decision** on the
Notifications Wayfinding map: the type registry `NotificationTypeDefinition` is **not built**,
because its justification was *"the service that renders is the service that declares what it needs"*
and caller-supplied templates break that premise. One endpoint serves both campaign and transactional
sends.

```json
{
  "type": "convocatorias.campaign.invite",
  "language": "es",
  "subject": "Te invitamos a {{evento}}",
  "body": "<html>… Estimado {{nombre}} … <a href=\"{{enlace_baja}}\">…</a> …</html>",
  "sharedData": {
    "evento": "Roadshow Madrid 2026",
    "lugar": "Madrid, España",
    "fecha": "12 de junio de 2026"
  },
  "recipients": [
    {
      "email": "juan@expedia.com",
      "data": {
        "nombre": "Juan",
        "enlace_registro": "https://app.…/publico/registro#t=<token>",
        "qr": "https://app.…/v1/public/registrations/<qr_code>/qr"
      }
    }
  ],
  "about": [
    { "sourceService": "convocatorias", "entityType": "campaign", "entityId": "…" },
    { "sourceService": "convocatorias", "entityType": "event", "entityId": "…" }
  ],
  "idempotencyKey": "…"
}
```

| Field | Required | Rule |
| ----- | -------- | ---- |
| `type` | yes | Plain lowercase dotted string, stored verbatim. **History label only**, no validation weight |
| `language` | yes | One of the six in `AllowedLanguage`, dictated by the `Event` |
| `subject` | yes | Substituted like the body |
| `body` | yes | Complete HTML, substituted and sent byte for byte |
| `sharedData` | no | String values only, applies to every recipient |
| `recipients` | yes | 1 to 1000 of `{ email, data? }`, string values only |
| `about` | yes | 1 to n of `{ sourceService, entityType, entityId }`, indexed, never sent to the provider |
| `idempotencyKey` | provisional | Semantics belong to the notifications map |

**`subjects` is renamed `about`.** The request now needs `subject`, the email subject line, and two
fields one letter apart meaning entirely different things is a trap someone falls into within a
month. `about` also reads as the query the client asked for: show me everything sent about the Feria
de Madrid.

**A campaign with no event** sends `sharedData` `{ asunto, mensaje }` instead of the event's three
values, gives each recipient no `enlace_registro`, and lists only the campaign in `about` (`03` §1.1).

**A key present in both `sharedData` and a recipient's `data` is a `400`**, never a precedence rule.
There is no legitimate reason to send one twice and a silent winner is a bug generator. Splitting
rather than merging is what keeps the event name off the wire a thousand times.

**Substitution is flat, literal and HTML-escaped** (`02` §5). Where rendering physically happens is
notification-service's own choice, and flat `{{key}}` is what preserves that choice, because it sits
inside every candidate engine's subset. It no longer maps onto `dynamic_template_data`, which an
earlier draft cited as the reason: dynamic templates are unreachable over SMTP (below), so in practice
notification-service substitutes in C# and hands the provider a finished body per recipient.

**Why the shape is forced rather than preferred**: the provider is SendGrid over **SMTP relay**
(`smtp.sendgrid.net`), confirmed by the client on 2026-09-12, and SendGrid's documentation carries a
verbatim Danger callout — *"The X-SMTPAPI does not support Dynamic Transactional Templates."* There is
no provider-side template path at all, so a caller-supplied body is the only shape available.

An earlier draft gave a different reason: that SendGrid's `content` is not in the per-personalization
override list, so per-recipient bodies are reachable only as template plus data. **That is a Web API
v3 fact and does not hold over SMTP**, where `X-SMTPAPI` `sub` substitutes straight into the DATA body
with no template involved. The conclusion was right and the reason has been replaced. Sourced in
`.scratch/notifications-map/research-sendgrid-smtp-relay.md`.

### 2.1 The response

`202 Accepted`, `Location: /v1/notifications/batches/{batchId}`, with one entry per recipient:

```json
{
  "batchId": "…",
  "results": [
    { "id": "…", "recipient": "juan@expedia.com", "status": "accepted" },
    { "id": null, "recipient": "no-such@@bad", "status": "rejected", "reason": "RECIPIENT_ADDRESS_INVALID" }
  ]
}
```

| Outcome | HTTP | Convocatorias does |
| ------- | ---- | ------------------ |
| Per-recipient `rejected` | 202 | `send_status = failed`. Terminal for that person |
| Per-recipient `accepted` | 202 | `send_status = handed_off` |
| Whole request fails, non-2xx or transport exception | any | Records nothing. Rows stay `pending`. A human resumes |

**The 202 is the line**: after it a history row exists, before it nothing does. A malformed request
is a `400` that stores nothing.

**`accepted` means notification-service persisted the row and will attempt it — not that SendGrid took
it.** Over SMTP a 1,000-recipient batch is roughly 1,000 sequential SMTP transactions, and SendGrid
publishes no throughput figure to size that against, so notification-service accepts, persists, answers
`202` and drains on a worker of its own. **There are two workers in the chain**: ours handing off
(§1.1) and theirs sending. Nothing in this contract changes and `handed_off` always meant handoff
rather than delivery, but the gap between `handed_off` and any real-world outcome is wider than a
reader of the earlier draft would assume. The only rejection available at accept time is a
syntactically invalid address, which is why `failed` is near-empty (`03` §3.1).

### 2.2 The cap

**1,000 recipients per call**, over the cap is a `400`, chunking is the caller's job. Size was never
what bound this: a body plus 1,000 data bags of roughly seven short strings is about 200 KB against a
30 MB limit. `{{qr}}` resolves to a **URL**, not bytes, which kills the one field that could have
changed this arithmetic. An earlier draft also cited SendGrid's 150-substitutions-per-personalization
cap; that is a Web API figure, and no provider substitution cap applies at all once
notification-service substitutes in C# (§2).

A 1,000-invitee campaign is **one** call and a 3,000-invitee campaign is **three sequential** calls.
They run on the worker (§1.1), so no HTTP timeout, nginx `proxy_read_timeout` or closed browser tab
bounds them. That is the constraint ADR-0018 was written to remove.

**No shared shell or branded wrapper.** The two jobs a wrapper would have had are already done inside
the body: artwork is in `body_html` and `{{enlace_baja}}` is a body placeholder. The body a caller
sends is the email that goes out, byte for byte after substitution.

## 3. The mailability re-check

`POST /v1/contacts/mailability` on crm-service, `{ ids: [...] }` capped at 1,000, returning
`[{ id, canBeEmailed, suppressionReason }]` and nothing else. A POST, because a 1,000-id query string
is not a URL. Neither the list filter nor the exact lookup answers "are these 1,000 ids still
mailable", and one call per contact would be 1,000 round trips inside the launch request.

- A recipient that comes back `canBeEmailed: false` goes to `send_status = suppressed`. Per-recipient
  suppression at handoff is **not** an error and carries no code; the rest proceed.
- **An id that does not come back is `suppressed`** with reason `contact_unavailable`, computed on
  our side because CRM cannot report on a row it will not return. A contact soft-deleted between
  selection and launch is the real case.
- **If the check itself fails, the whole handoff aborts before a single recipient is handed off.**
  The campaign stays `sending`, every row stays `pending`, and a human resumes. Explicitly not a
  partial send: half a campaign mailed against an unverified suppression list is the outcome this
  rule exists to prevent.

**Zero mailable recipients refuses the launch.** If the re-check comes back with nothing mailable,
the answer is `400 CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS`, the campaign stays in `draft`, and no row is
written. This runs at step 1, inside the request, so it is a refusal a person reads and not a
campaign that reports `sent` having mailed nobody.

**This reverses the approval-era rule**, which allowed it and marked every row `suppressed`. That
rule rested on a stated premise: *the campaign passed the check at submission*, so zero-mailable at
handoff meant everyone opted out while a director sat on the queue, which was a real outcome worth
recording. Submission is gone, the window is now the seconds between the launcher's screen and their
click, and the same condition no longer means what it meant. It means somebody is about to publish a
campaign to an empty room.

Zero recipient **rows** is the separate `400 CAMPAIGN_HAS_NO_RECIPIENTS` (`03` §4). Two codes,
because they are two different mistakes.

### 3.1 The unsubscribe flush comes first

A pending unsubscribe is **not** harmless the way a pending registration sync is. If the push to CRM
never lands, CRM still answers `canBeEmailed: true` for that person and the next campaign's check
mails someone who unsubscribed.

So the launch path pushes every unsynced unsubscribe to CRM **before** running the mailability check.
If the flush cannot complete, the launch fails `503 CRM_UNAVAILABLE`, which is the same answer §3
already gives, since CRM is unreachable either way.

**It stays inside the request**, ahead of the flip, where it can still refuse. Moving it onto the
worker with the batches would turn a refusable `503` into a campaign already `sending`. It is
bounded, triggered by a person pressing launch, and sits immediately before the read that depends on
it. It also means **no second suppression rule is written in convocatorias**. Rather than teaching
this service to reason over its own column, it makes CRM's answer correct before we ask the
question.

## 4. The gateway and the stub

```csharp
public interface ICampaignSendGateway
{
    Task<Result<SendBatchResult, BaseError>> SendAsync(SendBatchRequest request, CancellationToken cancellationToken);
}
```

Two implementations behind one setting:

| `Notifications__Transport` | Binds | Set in |
| -------------------------- | ----- | ------ |
| `stub` | `StubCampaignSendGateway` | `docker-compose.yml`; `docker-compose.dokploy.yml` today |
| `http` | the real typed client through `AddServiceClient` | `docker-compose.dokploy.yml` once notification-service exists |

**No environment branching in code.** `docker-compose.dokploy.yml` is one self-contained file
deploying both stage and production, so the two already run identical YAML and differ only in
injected values. An `IsProduction` branch would be a divergence hidden even from that file, and the
whole point is that stage behaves the way production will.

**Declared fail-fast with no default**, matching that file's own convention:

```yaml
Notifications__Transport: ${NOTIFICATIONS_TRANSPORT:?NOTIFICATIONS_TRANSPORT requerido (stub o http)}
```

A deploy that forgets it crash-loops identically in both environments. Binding the stub when
`Services__Notifications__BaseUrl` is **absent** was rejected as fail-open: the exact omission the
root `CLAUDE.md` warns about, a variable that reaches `docker-compose.yml` but never the Dokploy file,
would then make campaigns report `sent` while mailing nobody, silently, on the client's flagship
feature.

The key sits in **both** compose files so `scripts/compose-parity.sh` passes; the values differ, which
that script treats as legitimate since it compares keys only.

### 4.1 What the stub records

A `stub_notification` table in this service's own database, one row per recipient: `batch_id`,
`recipient_email`, `type`, `language`, `subject`, **the rendered body**, `status`, `created_at`. It
returns the full §2.1 response shape, per-recipient `status` included, or campaign tracking is never
exercised before the real service arrives.

- Log-only fails the acceptance criterion: an integration test cannot assert over a log line.
- `.eml` files need a named volume, and bytes with no named volume in the Dokploy file live in the
  container's writable layer and vanish on redeploy.
- **The table is behind the gateway interface and never joined to `campaign_recipient`**, or the
  temporary table quietly becomes schema.

It is created by an ordinary migration in **every** environment, staying empty where the transport is
`http`. A migration that branched on environment would break the single-schema-authority rule to
avoid one dead table.

### 4.2 The plaintext-token exposure, and its exit

A rendered body contains `{{enlace_registro}}`, which embeds the registration token that `06` stores
only as a SHA-256 hash precisely so the plaintext lives only in the email. **While stage runs `stub`,
stage holds plaintext tokens beside their own hashes.**

Accepted, with the exit pinned rather than remembered:

- A redacted body cannot be clicked through, which destroys both the demo and the only end-to-end
  test of the registration link.
- Stage carries no real invitee data, and anyone who can read the stage database already holds every
  invitee row.
- **Stage flips to `http` the day notification-service ships.** That is the whole reason the transport
  is a value and not an environment branch.
- **The migration that lands the real client drops `stub_notification`**, and the stub class and the
  `Notifications__Transport` setting go with it, leaving `Services__Notifications__BaseUrl` as the
  only required config.

## 5. Retry and resume

| Situation | Who fixes it | Route (`03` §7) |
| --------- | ------------ | --------------- |
| A batch call failed, rows still `pending` | a person | `POST /campaigns/{id}/resume-handoff` |
| The process was redeployed mid-send | a person | the same route, surfaced as `strandedCampaigns` in `07` §6 |
| Per-recipient `failed` | a person | `POST /campaigns/{id}/retry-failed` |

`resume-handoff` is `sending` only, because the unsent tail exists nowhere else. `retry-failed`
also works on a `sent` campaign, which is its normal case, and does not reopen it: see `03` §7.

Both take `campaigns:write` rather than `campaigns:launch`. They mail the tail of a list somebody
already launched, so they unstick rather than decide, and neither the copy nor the list changed.
Both enqueue onto the same worker and return `202`, on the same reasoning as the launch itself.

Resume walks the **unsent tail**, so convocatorias does not depend on idempotency in the common case.
The ambiguous one is a **timeout**, where we cannot tell whether the batch landed; that is the
requirement handed to the notifications map's idempotency ticket.

## 6. What convocatorias stores

**No rendered-body snapshot per campaign** (`02` §6).

The finding behind this, sharper than a storage-cost argument: **per-recipient `data` carries a live
credential.** `{{enlace_registro}}` embeds the registration token, so any store that keeps `data`
verbatim holds plaintext tokens for every invitee, permanently, and undoes the hash-only rule from the
outside.

Handed to the notifications map's send-history ticket as a **security** trade rather than a storage
one: **store the body once per batch, retain no per-recipient `data` values at all.** One body per
batch answers the client's actual ask, because the only thing differing between two recipients is a
greeting and a link. The cost accepted is that history can show what a campaign said, not the precise
bytes one named person received.

## Definition of done

- Launch returns `202` with a `Location` header before any batch is posted, and the campaign is
  `sending` at that moment with `launched_at` stamped and `sent_at` null.
- Launch against the notification stub eventually moves every recipient to `handed_off` and the
  campaign to `sent`, with `sent_at` stamped by the worker.
- **The batch loop survives an aborted request.** Cancel the client connection immediately after the
  `202` and assert every recipient still reaches `handed_off`. No `CancellationToken` bound from a
  handler reaches the worker.
- A stub returning one per-recipient `rejected` leaves that row `failed` and the rest `handed_off`,
  and the campaign still reaches `sent`.
- A stub returning `503` on the second batch leaves batch one `handed_off` and the rest `pending`,
  the campaign `sending`, and a re-post hands off only the remaining rows. Prove the first 400 are not
  mailed twice.
- A mailability response missing one requested id leaves that recipient `suppressed` with reason
  `contact_unavailable`.
- A CRM outage at the mailability check leaves **zero** recipients handed off.
- Launching a campaign whose event was cancelled is refused, and mails nobody.
- A launch whose mailability re-check returns nothing mailable is
  `400 CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS`, leaves the campaign in `draft`, and writes no row.
- An unsynced unsubscribe is pushed to CRM before the mailability call, proven by asserting request
  order against the CRM stub.
- A key present in both `sharedData` and a recipient's `data` is rejected before anything is sent.
- 1,001 recipients are sent as two calls; the second carries the remainder.
- `Notifications__Transport` absent crash-loops the service on startup rather than defaulting.
- `StubCampaignSendGateway` writes one `stub_notification` row per recipient with a rendered body, and
  no foreign key exists between that table and `campaign_recipient`.
