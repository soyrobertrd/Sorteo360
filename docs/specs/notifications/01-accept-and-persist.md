# 01 — Accept and persist: the wire contract, validation, idempotency, the record

`POST /v1/notifications` from end to end, up to and including the `202`. Nothing here sends an
email; `03` does that.

**The rule this whole spec serves: the `202` is the line. After it a history row exists, before it
nothing does.** A malformed request is **our** bug — it answers `400` and stores nothing. A world
failure is stored and answers `202`. The client asked to browse the emails we sent, not the bugs we
wrote.

## 1. The request

```json
{
  "type": "events.quote.submitted",
  "language": "es",
  "fromAddress": "no-reply@mitur.gob.do",
  "subject": "Cotización pendiente de aprobación — {{evento}}",
  "body": "<html>… {{categoria}} … <a href=\"{{enlace}}\">Ver el evento</a> …</html>",
  "sharedData": {
    "evento": "Feria de Turismo Madrid 2026",
    "categoria": "Ambientación",
    "candidatos": "3",
    "sometidoPor": "María Pérez",
    "enlace": "https://app.pgti360.mitur.gob.do/eventos/xyz-789"
  },
  "recipients": [
    { "email": "maria@mitur.gob.do", "name": "María González" },
    { "email": "jose@mitur.gob.do" }
  ],
  "about": [
    { "sourceService": "events", "entityType": "event", "entityId": "xyz-789" },
    { "sourceService": "events", "entityType": "quote", "entityId": "abc-123" }
  ],
  "idempotencyKey": "events:quote:abc-123:attempt:2:submitted"
}
```

| Field | Required | Rule |
| --- | --- | --- |
| `type` | yes | Lowercase dotted `<service>.<thing>.<what happened>`. Stored verbatim. **A history label only** — no catalog, no lookup, no foreign key |
| `language` | yes | One of the six `AllowedLanguage` values. **No default** |
| `fromAddress` | yes | The sender. This service never looks up which office is sending |
| `fromName` | no | Display name for the sender |
| `subject` | yes | Substituted exactly like the body |
| `body` | yes | A complete HTML email. Substituted and sent byte for byte |
| `sharedData` | no | String values only. Applies to every recipient |
| `recipients` | yes | 1..1000 of `{ email, name?, data? }`. String values only. `name` is at most 200 characters, trimmed; blank counts as absent |
| `about` | yes | 1..n `{ sourceService, entityType, entityId }`. Indexed. **Never leaves the platform** |
| `idempotencyKey` | yes | See §3 |

**Values are strings only, everywhere.** `"candidatos": "3"`, `"monto": "USD 45,000.00"`. Money
decides it: ADR-0002 gives USD/EUR with no FX, and a number on the wire hands the currency symbol
and the separators to a substitution step that cannot unit-test them. Strings force that formatting
into C# next to the `Money` type that already knows the rules. Cost accepted: `"3"` reads oddly for
a count, and nothing can compare a value numerically.

**`type` is a plain string, never an enum serialised as a number** — reordering an enum's members
would silently rewrite the meaning of every historical row. An enum inside each *caller* is a fine
typo-guard; the contract across the gap is the string.

**`name` is a fact of the send, like the subject** (added 2026-09-23, PGTI-104). It goes in the `To`
header (`María González <maria@mitur.gob.do>`) and into the history row, frozen as it was that day.
It is not the `{{nombre}}` placeholder, which stays in `data` and may carry only a first name; and it
does not break the ids-not-names rule, because nobody resolves it later. Absent means the history
shows the address alone. It is part of the fingerprint in §3, so the same key with a different name
is a `409`.

**No `cc`, no `bcc`, ever.** One email per person. Over SMTP this is native behaviour rather than a
special case, which is why a history row always means exactly one person.

> **Normative caller obligation, enforced by nothing:** personal data and any credential go in
> `data`, never inlined into `body`. Scanning the body at accept time was rejected — it
> false-positives on a legitimate `info@mitur.gob.do` footer and false-negatives on a plain person's
> name, and a check that can be wrong in both directions is worse than a rule a person can read.
> Say this out loud in the API documentation rather than implying a guarantee we do not have.

## 2. Validation, and the two `400`s that matter

Validated in this order, before anything is persisted. Every failure is `ProblemDetails` from the
chassis `BaseError.ToProblem()` carrying a machine `code`; clients branch on the code and render
their own Spanish.

| Check | Code |
| --- | --- |
| Shape, required fields, `language` outside `AllowedLanguage` | `NOTIFICATION_PAYLOAD_INVALID` |
| More than 1,000 recipients | `NOTIFICATION_RECIPIENT_CAP_EXCEEDED` |
| A key present in both `sharedData` and a recipient's `data` | `NOTIFICATION_DATA_KEY_COLLISION`, naming the key |
| A `{{key}}` in `subject` or `body` with no value anywhere | `NOTIFICATION_PLACEHOLDER_UNRESOLVED`, naming the key — see `02` §2 |
| `idempotencyKey` reused with a different body | `409 NOTIFICATION_IDEMPOTENCY_KEY_REUSED` — see §3 |

**A colliding key is a `400`, never a precedence rule.** There is no legitimate reason to send one
value twice, and a silent winner is a bug generator.

**The 1,000 cap is ours, and the reason has to be written as ours.** It is *not* SendGrid's SMTP
limit: over SMTP the hard cap is 10,000, with an *"approximately 1,000 recipients"* best practice and
an explicit instruction to go *"even smaller"* when substitutions are numerous — which is exactly our
case. 1,000 is defensible and deliberate. Over the cap is a `400`; chunking is the caller's job.

**Unknown top-level fields and unknown `data` keys are ignored, never rejected**, so a newer caller
can always talk to an older service. A key becomes *required* in three ordered steps: the template
renders without it, callers start sending it, then the check promotes it. Lockstep deployment was
rejected as the written rule despite usually working at this scale — the dance costs one extra
deploy on rare occasions, while forgetting lockstep drops notifications in a window nobody watches.

> **The escape hatch, so nobody reinvents `.v2`: a notification that changes meaning is a different
> notification and gets a different name.**

## 3. Idempotency

**Caller-minted and derived from a natural key, never random** — a random key regenerated on a retry
is not a key at all.

> **Normative: the idempotency key names the send, not the thing the send is about.** It has been
> drawn one level too coarse twice during design, and both times the symptom was a missing email
> with no error anywhere.

events-service's key is `events:quote:{quoteId}:attempt:{n}:{moment}`. The `events:` prefix is not
decoration: the key space is global across every caller, and `quote:` alone is a word convocatorias
could reasonably reach for.

**Why the key cannot be coarser, recorded so nobody simplifies it back:** keyed on the *quote*,
attempt 2's alert is swallowed as a duplicate of attempt 1's. Keyed on the *attempt*, the reject
alert is swallowed as a duplicate of that attempt's submit alert. Both are silent on both sides.

**It never expires.** The keys are derived, so the space cannot exhaust; the history rows are kept
indefinitely anyway as a client feature; and a TTL has exactly one observable effect — convocatorias
resumes a stranded campaign after the window closes and re-mails 400 people. A deliberate resend is
unaffected, because a new attempt carries a new key.

**A repeat answers `202` replaying the original `batchId` and per-recipient ids. Never `409`.** The
caller was told never to branch on the response, and a `409` is a failure status the caller then
swallows and logs — a false alarm about a request that worked. Replaying is simply true: that batch
exists, here are its ids. **This is what makes the chassis's blind retry safe**, and that is the
whole point: `ServiceResiliencePipeline.Build` is **verb-agnostic** — it retries 408, 429, 5xx,
`HttpRequestException` and `TimeoutRejectedException` with no safe-to-repeat check — so the shipped
chassis already re-POSTs today. Idempotency is not a refinement on a safe transport; it is what
makes the transport safe for writes, and every future `AddServiceClient` caller inherits the same
exposure.

Two mechanics, both required:

- **Two identical POSTs racing** are settled by the **unique index on the key**. The loser catches
  the violation, re-reads by key, and returns the winner's response. No locking, no coordination.
- **The same key with a different body is our bug**, and our bugs are loud. Store a **SHA-256 of the
  canonical request** beside the key; a mismatch answers `409 NOTIFICATION_IDEMPOTENCY_KEY_REUSED`.
  One column, catching a class of bug that is otherwise completely invisible.

## 4. The record — four tables

| Table | Holds | One per |
| --- | --- | --- |
| `notification_batch` | `type`, `from_address`, `from_name`, `subject`, **`body` pre-substitution**, `language`, `idempotency_key` (unique), `body_sha256`, `tracking_expected`, `trace_id`, `created_at` | one accepted POST |
| `notification_about` | `source_service`, `entity_type`, `entity_id` | one correlated entity |
| `notification_recipient` | opaque GUID id, `email`, `name` (nullable, max 200), `delivery_status`, `recipient_action`, `attempt_count`, `next_attempt_at`, `queued_at`, `sent_at`, `last_error`, `sg_message_id`, `smtp_queued_as` | one person |
| `notification_event` | `sg_event_id`, `event_type`, `occurred_at`, raw JSON, `recipient_id` **nullable** | one webhook event |

> **Normative: `body` is stored pre-substitution.** The caller's body still carries its
> `{{placeholders}}`, and a live credential — convocatorias' registration token — lives only in
> `data`. So no token can ever be inside a stored body. **The safety is structural, not a policy:**
> there is no scrubbing rule to get wrong, no redaction code to review, and nothing a later edit can
> quietly weaken.

**No `data` is stored at all — not values, not keys.** Keys are already visible in the stored body,
sitting there as `{{nombre}}`. The only thing lost is a key present in `data` but absent from the
body, which changed nothing about the email anyone received.

**The accepted cost, stated plainly:** the history shows what a batch *said*, not the precise bytes
one named person received. This is also why there is no resend (`05` §4).

**Orphans are not a fifth table.** An orphan is a `notification_event` row with a null
`recipient_id`. A linked event and an orphan are the same kind of thing, so they share one bucket
and one query — and an orphan can later be linked by filling the column in, which a separate table
would make a migration.

> **Normative: `tracking_expected` is stamped on the batch row at insert from
> `SendGrid__EventWebhook__Enabled`, never read as a global flag at query time.** A row written
> while no webhook existed stays honest forever, even after someone turns the webhook on later. See
> `05` §3 for the invariant it carries.

`about` is indexed on `(entity_type, entity_id)`. That index is the reason the correlation list
exists: *"show me everything sent about the Feria de Madrid"* is the client's actual question, and a
free-text correlation field would degrade it to a `LIKE` the moment two callers formatted it
differently.

**`about` never leaves the platform.** Entity ids are not put in `unique_args`, which carries only
the recipient's own opaque GUID — and `unique_args` **cannot be erased once sent**.

## 5. The response

`202 Accepted`, `Location: /v1/notifications/batches/{batchId}`.

```json
{
  "batchId": "…",
  "results": [
    { "id": "…", "recipient": "maria@mitur.gob.do", "status": "accepted" },
    { "id": null, "recipient": "no-such@@bad", "status": "rejected", "reason": "RECIPIENT_ADDRESS_INVALID" }
  ]
}
```

> **`accepted` means "we persisted it and we will attempt it." It does not mean SendGrid took it,
> and it never means delivered.** Under SMTP the provider leg happens on a worker, after this
> response.

**The only rejection available at accept time is a syntactically invalid address.** So `rejected` is
near-empty in practice. That is not a gap: convocatorias justified it as the **resume cursor**, not
as a common outcome.

> **A suppressed address is `accepted` here, not `rejected`.** Suppression is checked by the worker
> before it opens the socket (`03` §7), so the row exists and is later marked
> `delivery_status = dropped` with the reason in `last_error` — **recorded, never silently dropped**.
> Rejecting it at accept time would mean returning `id: null` for a person the history is supposed to
> account for.

**Ids only come back — no full record and no aggregate status.** Fire-and-forget means the caller
does not *wait*, not that it learns *nothing*: the id is the thread to pull when the client reports
a missing email six weeks later. A full record, or a top-level `status` that would read `pending`
every time, is an invitation to branch on it — which is the coupling fire-and-forget exists to
prevent.

**Callers log the ids; only events-service also stores them**, because ADR-0011's attempt row is
business state a quote already depends on (`06` §6). A notification id has no business meaning of
its own, so a column for it elsewhere would be a migration for a debugging aid.

## 6. Authorization

`.RequireAuthorization` on the `notifications:send` function role. **Never the fallback policy**, and
never a composite name in code. See `00` §8 for why, and for the normative line that this role is
never granted to a person.

## Definition of done

- A valid POST returns `202` with the `Location` header, one result per recipient, and creates one
  `notification_batch`, one `notification_about` per entry and one `notification_recipient` per
  person.
- A second POST with the same `idempotencyKey` and the same body returns `202` with **the same
  `batchId` and the same per-recipient ids**, and creates nothing.
- The same key with a different body returns `409 NOTIFICATION_IDEMPOTENCY_KEY_REUSED`, and so does
  the same key with only a recipient's `name` changed.
- A recipient `name` is stored trimmed, a blank one as null, and one over 200 characters is a 400
  `NOTIFICATION_PAYLOAD_INVALID` that stores nothing.
- **Two identical POSTs fired concurrently produce one batch and two identical `202`s, not a 500** —
  enforced by the unique index, not by a pre-check.
- Each of the five validation failures in §2 returns its code and **creates nothing**. Assert the
  "creates nothing" half, not only the status.
- A key in both `sharedData` and a recipient's `data` is a 400 naming the key.
- 1,001 recipients is a 400; 1,000 succeeds.
- An unknown top-level field and an unknown `data` key are both **ignored**, and the request
  succeeds.
- The stored `body` still contains its `{{placeholders}}`, and **no row anywhere holds a `data`
  value** — assert by inserting a recognisable token in `data` and grepping every column of every
  table for it.
- `tracking_expected` on a batch matches `SendGrid__EventWebhook__Enabled` at insert time, and does
  not change when the setting is flipped afterwards.
- A token with no `notifications:send` role gets 403; no token gets 401; a human composite holding
  the role does not exist in the realm JSON.
