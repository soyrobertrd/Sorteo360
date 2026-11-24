# 05 — The send history read API

The client's feature. *"Let me browse every email we sent."* This is the requirement that decided
the service boundary in the first place, so it is not a reporting afterthought.

## 1. The endpoints

| Endpoint | What it is |
| --- | --- |
| `GET /v1/notifications` | Paged **recipient** rows. The main surface — one row per email actually sent |
| `GET /v1/notifications/batches/{batchId}` | One batch: stored body, subject, and its recipients. **Already the `Location` target of the `202`**, so it exists regardless |
| `GET /v1/notifications/orphan-events` | Paged unlinked events |

Filters on the list: `type`, `deliveryStatus`, `recipientAction`, `email`, `entityType` +
`entityId`, `sourceService`, `sentFrom` / `sentTo`, `id` (repeated, up to 100), `page`, `pageSize`.

> **Added 2026-10-03 (PGTI-177).** `id` is the per-recipient id the `202` returned. It is how a
> caller reads the delivery of its own sends in one call: convocatorias keeps the id on each
> campaign recipient and asks for a page of them at once. More than 100 is a `400`.

*"Show me everything sent about the Feria de Madrid"* is `entityType` + `entityId`, running on the
`(entity_type, entity_id)` index from `01` §4. **That query is the reason the correlation list
exists**, and it is answerable.

**Paging copies ~~crm-service~~ events-service** — `PagedResponse<T>` as
`{ total, page, pageSize, items }`, default `pageSize` 25, maximum 100, over-maximum a `400`.

> **Corrected 2026-09-15.** crm-service is the wrong service to name here, and copying it verbatim
> would have produced the opposite of what this spec wants: it answers `totalCount`, not `total`,
> and it clamps an oversized `pageSize` instead of rejecting it. What this service built is the
> events-service dialect, which is also what `04-read-api.md` §3 asks a new service to copy. The
> `25` is this spec's own, against the siblings' `20`, and is deliberate.

**Orphans get their own named endpoint rather than a `?recipientId=null` filter.** An orphan names
no row, so it can never appear in the recipient list at all; and `04` established these are often
**bounces**, the event the client cares most about. Burying them behind a null filter is how a
bucket nobody opens gets built.

**No free-text search.** `email` is an exact match. Subject search needs a text index, which is
machinery with nobody yet asking for it — easy to add later, awkward to remove.

## 2. Who may read it

| Reader | Sees |
| --- | --- |
| A human with `central_admin` | Everything, via `notifications:read` |
| A calling **service account** | **Only rows whose `about.source_service` is itself**, same role |

**OPT scoping is unreachable, not declined, and the reason matters more than the answer.** This
service knows no `opt_code` (it holds no domain knowledge) and has no outbound client to ask
events-service *may this person see this evento?* Adding an OPT column would reinstate exactly the
knowledge two decisions deliberately removed, in the one service chartered to hold none.

**The coordinator is not left blind by this.** A failed send shows on the quote they submitted, and
that status ships in `QuoteDetailResponse.Attempts` (`06` §6). They will never open an admin screen
anyway.

**The service-account read is not speculative scope.** `docs/specs/convocatorias/07-tracking-and-reconciliation.md`
carries no `delivered` / `bounced` field anywhere and says in writing that delivery feedback *"waits
on the notifications webhook question"* — a question now answered yes. Filtering on `source_service`
needs no new domain knowledge, because the field is already on the row.

**The role is a function role and the code never names a composite** (ADR-0010). Start
`notifications:read` in `central_admin` alone; widening it is a one-line realm edit and no C#.

## 3. What the screen may not imply

Every recipient in a response carries **`deliveryTrackingAvailable: true | false`**, derived from
the batch's `tracking_expected`.

> **Normative: when `tracking_expected` is false, `delivery_status` never passes `sent`.** Without
> this written down, a future reader sees a permanent `sent` and goes hunting for a stuck queue that
> does not exist.

When it is false the screen shows *"delivery tracking not available in this environment"* rather
than reading `sent` as a final state. **Stage is such an environment by design** (`03` §8), so this
is the state the client is demoed on, not a hypothetical edge case.

### Client-visible versus ours

Both are served on the same endpoints — the only human reader is `central_admin` — but the split is
named rather than implied, because nothing on a screen should be built to depend on the second
column.

- **Client-visible:** `type`, `subject`, body, `language`, from-address, recipient email, recipient
  `name` (null when the caller sent none; the screen then shows the address, never a dash),
  `delivery_status`, `recipient_action`, `queued_at`, `sent_at`, the `about` list.
- **Ours, for debugging:** `body_sha256`, `sg_message_id`, the `250 Ok: queued as` string,
  `attempt_count`, `last_error`, and the raw event JSON. The queued-as token in particular is **not
  a proven join key** — it is the first thing SendGrid support asks for, and that is all.

> **Note 2026-09-23 (PGTI-104).** The client asked to see why a send failed, so the Reportes screen
> shows `last_error` and `attempt_count` after all (plan `notifications-frontend-wiring`, decision
> 7). They stay ours in meaning: raw SMTP text and a retry counter, not copy written for the client.
> The same change added the recipient `name` to both the list item and the batch's recipients.

## 4. There is no resend, on a fact rather than on scope

**We store no `data`, so a placeholder-bearing batch cannot be rebuilt.** A resend would push out
something different from what the history displays: a broken greeting, and `{{enlace_registro}}`
either sitting there as literal text or empty.

> **A button that silently sends a different email than the one on screen is worse than no button.**

Re-triggering already exists upstream in both callers — events-service re-submits the quote,
convocatorias has `resume-handoff`. Neither needs us.

Consequence worth having: **the history is read-only**, so this surface has no write authorization
to design at all.

## 5. Retention and erasure

> **Normative: one policy for rows and raw events together — indefinite, no purge job in phase 1.**
> Splitting them produces events pruned at 90 days beneath history rows kept forever.

Three reasons: volume is trivial (a handful of cotización alerts a day; ~1,000 invitees a few times
a year); the privacy surface is **one column**, the email address, after `01` §4's reframe; and
**MITUR is a government agency, where a records rule is far likelier to be a *minimum* keep-period
than a maximum** — so deleting on a schedule we invented could be the compliance error rather than
the fix. The client is asked for a **minimum**, and it blocks nothing.

**Erasure is not built, because there is no caller.** crm-service's own erase is explicitly
*"Deferred 2026-09-04, not build-ready"*, with no `erased_at` column and no endpoint. The shape is
recorded for when it lands:

> **The history row survives with the email address redacted. It is never deleted.** Deleting
> destroys the evidence that mail was sent, which is the thing a records regime wants kept, while
> redacting removes the personal data. It mirrors crm's own decided rule — one pattern, not two —
> and after `01` §4 it is **one `UPDATE` on one column**.

**Our erasure path will not call SendGrid's `POST /v3/recipients/erasejob`, and the reason outlives
"no caller yet":** that endpoint is **Web API v3**, our transport is SMTP relay, and we have asked
the client for a key scoped to **Mail Send only**. A Mail-Send-scoped key cannot reach it. Wiring it
in would widen a key scope narrowed one decision ago.

## 6. Deliberately not on this surface

**The suppression list.** `03` §7 gives this service one, but a suppression list is not a send
history. Reading it, per-type preferences, the GDPR-consent interaction and who may un-suppress an
address are open questions — named here so they are not smuggled in.

## Definition of done

- `GET /v1/notifications` returns `PagedResponse<T>` as `{ total, page, pageSize, items }`, defaults
  to `pageSize` 25, rejects above 100 with a `400`.
- Every filter in §1 works, and `entityType` + `entityId` returns every row of every batch about
  that entity — **assert with two batches from two different `type`s about one event**.
- `email` is an exact match and does not do a partial or case-folded `LIKE`.
- A list item and a batch recipient both carry `name`: the one sent with the recipient, or null.
- `GET /v1/notifications/batches/{batchId}` returns the **pre-substitution** body, and the `Location`
  header from a `202` resolves to it.
- `GET /v1/notifications/orphan-events` returns unlinked events and they appear **nowhere** in the
  recipient list.
- A `central_admin` token sees rows from every `source_service`; a `service-account-events-service`
  token sees **only** `events` rows and cannot read a convocatorias batch by id either.
- A token without `notifications:read` gets 403; no token gets 401.
- Every recipient carries `deliveryTrackingAvailable`, and a row on a batch with
  `tracking_expected = false` never reports a `delivery_status` past `sent` — **assert by feeding it
  a `delivered` webhook event and checking the status does not move**.
- There is no POST, PUT, PATCH or DELETE on any route in this spec.
