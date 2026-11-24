# Campaigns and Recipients — Spec

The core model. A campaign is one email going out to one list of people for one event, in one
language, from one template. It moves through four states, its recipients through four, and the two
axes never derive from each other.

**Nobody approves a campaign.** Whoever holds `campaigns:launch` launches it, in one call, and it
goes. The client removed the approval step on 2026-09-07, after the wayfinding map had already
decided the opposite; what that reversal cost and what it would cost to put back is recorded on the
closed ticket "Define the campaign state machine and the deferred approval seam" and in ADR-0017.

The reference vertical is `ServiceSupplier` and `Quote` in events-service. `QuoteStatus` is still the
shape the state guards copy, so the campaign guards read like code somebody already reviewed.
`QuoteDecision`, `QuoteDecisionOutcome` and `IApprovalGate` are **not** copied here: there is no
decision to record.

## 1. The three tables

| Table | Grain | Holds |
| ----- | ----- | ----- |
| `campaign` | one per send, many per event | `event_id` (nullable, §1.1), `template_key`, `language`, `state`, `launched_by`, `launched_at`, `sent_at`, `subject`, `message` |
| `campaign_recipient` | one per contact per campaign | `contact_id`, name and address snapshot, `send_status`, the two tokens, the two unsubscribe columns |
| `campaign_exclusion` | one per person filtered at selection | `campaign_id`, `contact_id`, name snapshot, `reason`, `excluded_at` |

Three tables, and no fourth. **There is no launch-event table.** `launched_by` plus `launched_at` on
the campaign row answer who pressed it and when, which is the whole question a launch audit was ever
going to answer, and one row per campaign cannot disagree with itself the way a parallel history
table can drift from the state column beside it.

Two constraints carry the grain rather than leaving it to the code: `campaign_recipient` is unique on
`(campaign_id, contact_id)`, and `campaign_exclusion` is unique on the same pair. A person is on a
campaign once, as a recipient **or** as an exclusion and never as both, which is what keeps every
count over those two tables honest.

**A registration hangs off the event, never the campaign** (`06`). A person who received two
campaigns must not be able to register twice.

**Ids plus a snapshot, deliberately both.** `contact_id` is identity; the name and address are what
was actually mailed. If someone edits their email in CRM next March, the record of the September send
must not silently rewrite itself. This is why reading a sent campaign never calls CRM at all.

### 1.1 A campaign with no event

Amended 2026-10-03 (ADR-0026, PGTI-174). **A campaign may have no event**: a direct message to one or
more contacts, with no registration link and nothing to register for. It keeps every consent and
suppression check an event's campaign has, because it is a campaign and goes through the same launch.

The rule is one sentence: `event_id` is null **if and only if** `template_key` is `direct_message`,
and then `subject` and `message` are set; an event campaign has neither. Create and PATCH refuse a
breach with `400 CAMPAIGN_DIRECT_MESSAGE_MISMATCH`, and `ck_campaign_direct_message_is_whole` holds
it in the table. The event is not editable, so PATCH cannot move a campaign across the line either
way. `message` is plain text up to 10,000 characters, `subject` up to 200.

## 2. Campaign states

Four. `draft` is where a campaign is built, `sending` is where it is being handed off, and the two
terminals say how it ended:

| From | To | On |
| ---- | -- | -- |
| `draft` | `sending` | launch |
| `draft` | `cancelled` | the campaign is abandoned |
| `sending` | `sent` | no recipient row left `pending` |
| `sending` | `cancelled` | someone stops a send already in flight |

`sent` and `cancelled` are terminal.

**There is no state between `draft` and `sending`.** `pending_approval` is gone, and so are the
`submit` and `withdraw` acts that moved a campaign in and out of it. A campaign is either being
built or on its way out, and there is no resting state in which it waits for a person.

**`cancelled` from `sending`** is allowed. Refusing it means the only way out of a campaign you have
realised should not go out is to finish mailing everyone. Allowing it leaves some mailed and some
not, which is ugly but true, and the recipient rows say exactly who is in which group. **Untouched
rows stay `pending`** rather than being rewritten: a `pending` row under a `cancelled` campaign reads
correctly as "we intended to mail this person and then stopped".

**`sent` is not set by hand.** It is reached when every recipient row is terminal, meaning
`handed_off`, `failed` or `suppressed`, with none left `pending`. The worker that drains the batches
is what makes that flip (`04` §1, ADR-0018), so nothing sets it inside the launch request.

**A failed launch writes nothing.** Every refusal in §4 leaves the campaign in `draft` with no state
change, no `launched_by`, no `launched_at` and no recipient row touched. There is no failed-launch
record to read, because a campaign still sitting in `draft` is the record.

## 3. Recipient states

`campaign_recipient.send_status` tracks the handoff and nothing else:

```
pending ──> handed_off
        ├─> failed      (with a reason)
        └─> suppressed
```

Three things are deliberately **off** this axis:

- **`registered`** is the existence of a `registration` row pointing at this recipient, derived at
  read time. Folding it in loses "we emailed them and they replied", and that conflation is where the
  mock's `WAITLIST` and its scoring system came from.
- **`opened` and `clicked`** need the provider webhook, and nobody asked for open tracking.
- **Delivery** needs the same webhook. Day one a campaign knows what it handed off and never what
  landed, so `07` says `entregado al proveedor` and never `entregado`. `handed_off` is now two removes
  from delivery rather than one: notification-service answers `202` when it has persisted the row, and
  sends over SMTP on a worker of its own afterwards (`04` §2.1).

### 3.1 What `failed` actually means

**Only a per-recipient rejection returned by the sender.** In practice that is a syntactically bad
address that never passed through the upload staging screen because the recipient came straight from
CRM by classification, where `Email` is nullable today. Hard bounce after acceptance, mailbox full,
greylisting and spam foldering are all invisible until the webhook lands.

So `failed` will be close to empty in practice, and under the stub it never fires at all. **It earns
its place as the resume cursor.** A thousand recipients is several batched calls; if batch five of
ten gets a `503`, rows 1 to 400 are `handed_off` and 401 to 1000 are `pending`, and the retry resumes
at 401 instead of mailing the first 400 twice. Without a per-row status a retry is either a double
send or nothing.

**Transport failure therefore leaves rows `pending`, not `failed`.** `failed` means the sender looked
at this specific person and refused them — and under SMTP that look happens at accept time, before any
SMTP transaction, so in practice it catches only a malformed address. A provider-side refusal that
happens during the send lands in notification-service's own history, not here, until the webhook
question is answered (`07` §5). Retrying failed recipients is a manual per-campaign action
that relaunches nothing, since neither the copy nor the list changed.

### 3.2 Why `suppressed` is on the axis

Suppression runs **twice**, and removing approval did not remove the second run. At selection a
suppressed contact is filtered out and never materialised, so the recipient count stays "the number
of people we meant to mail". At handoff it runs again, because the two moments are not the same
moment and mailing someone who has opted out in between is the one failure ADR-0013 forbids
degrading into.

The window is now minutes rather than a director's queue, but a minute is enough. Someone can click
the unsubscribe link in yesterday's campaign while today's list is being reviewed, and the flush in
`04` §3.1 is what makes that click visible to CRM before we ask. Nothing about the check depended on
approval; what it depends on is that selection and handoff are two separate reads.

That second check produces a recipient who was selected and must not be mailed, and `suppressed` is
where it goes. It also answers the launcher's obvious question: why 1,043 selected became 1,041 sent.

## 4. Launch

One person, one call, `draft` to `sending`. `POST /campaigns/{id}/launch`, guarded by
`campaigns:launch`.

**The verb is launch, never send** (`convocatorias-service/GLOSSARY.md`). A Campaign is launched; a
CampaignRecipient is sent to. `send_status`, `ICampaignSendGateway` and `sent_at` all live at the
recipient grain and keep that word; naming the campaign-grain act `send` too would put one word on
two grains in a service whose whole tracking read depends on telling them apart.

What the call records: `launched_by`, the caller's Keycloak id as a bare Guid with no foreign key
since identity is Keycloak's, and `launched_at`. `sent_at` is separate and is stamped later, by the
worker, when the last recipient row goes terminal. The gap between the two is exactly the handoff,
which is a number worth being able to read.

**Six refusals, all of them leaving the campaign in `draft`:**

| Condition | Code | Status |
| --------- | ---- | ------ |
| Its template has no variant in its language | `CAMPAIGN_TEMPLATE_NOT_FOUND` | `400`, §4.1 |
| The campaign has no recipient rows at all | `CAMPAIGN_HAS_NO_RECIPIENTS` | `400` |
| It has recipient rows and none of them is mailable | `CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS` | `400` |
| The list changed under the launcher | `CAMPAIGN_RECIPIENTS_CHANGED` | `409`, §5 |
| The event it convokes is cancelled | `CAMPAIGN_EVENT_CANCELLED` | `409`, `04` §1 step 4 |
| The event has no value for a placeholder the email names | `TEMPLATE_EVENT_VALUES_MISSING` | `400`, `02` §2.2 |

The last two arrive from the event read, which is step 4 of `04` §1 and the only guard there is on
the event. They join this family rather than starting their own: for the launcher they are the same
kind of answer, "not yet, and here is what to fix".

**Two codes, not one**, because they are two different mistakes and the screen has to say two
different things. The first means you never selected anybody. The second means the 240 you selected
are all suppressed now, which is a real and alarming thing to be told, and collapsing it into "no
recipients" would send a coordinator hunting for a list that is right there.

**A campaign that would mail nobody is refused rather than recorded.** Both checks run inside the
request, before the flip. This reverses what the approval-era spec said about the handoff, where
zero-mailable was allowed to complete on the grounds that everyone had opted out during the approval
window. There is no approval window any more; the same condition now means the launcher is about to
publish a campaign to an empty room, and telling them beats recording it. See `04` §3.

Everything else about what happens after the flip is `04`: the flush, the re-check, the event read,
the `202`, and the worker that drains the batches.

### 4.1 A language with no template

Only `es` is seeded today, and `02` §3 rules out falling back to Spanish. So a campaign whose
`(template_key, language)` pair has no row in `template` cannot be rendered, and **three calls refuse
it with `400 CAMPAIGN_TEMPLATE_NOT_FOUND`**, carrying `templateKey` and `language` as ProblemDetails
extensions so the screen can name the missing copy:

- **Create** checks the pair it was given, and creates nothing.
- **PATCH** checks the pair as it would be after the edit, taking the untouched field from the row.
  Changing only the language to one the current template lacks is refused, and the row is left as
  it was. The draft guard runs first, so a campaign past `draft` still answers `409
  CAMPAIGN_NOT_DRAFT`.
- **Launch** checks it in step 1 of `04` §1, right after the state guard and before the recipient
  checks, the CRM or the event read. It catches the draft saved before create and PATCH refused it,
  and the one whose variant disappeared afterwards.

The lookup is the same exact match the worker makes when it renders. Before this check, the launch
answered `202` and the worker then failed with `CAMPAIGN_NOT_RENDERABLE`, leaving the campaign
stranded in `sending` with nobody to tell. `400` rather than `404`: the campaign exists, and what is
wrong is the template it names, the same "not yet, here is what to fix" family as
`TEMPLATE_EVENT_VALUES_MISSING`.

## 5. Freeze points

- **Recipient list membership freezes at launch.** Not before, because there is no earlier gate left
  to freeze it at. The campaign is editable right up to the call and frozen from the flip onwards.
- **Name and address snapshot is taken at handoff.** Membership is what was launched; the address is
  what we actually mailed.
- **`template_key` and `language` lock at launch**, on the same reasoning. There is no body snapshot
  to freeze (`02` §6).
- **Arte has no freeze point at all**, since it lives inside `body_html`.

**A campaign in `sending` is read-only**: no recipient changes, no template change, no language
change, refused with a machine code. `sent` and `cancelled` likewise. Only `draft` accepts an edit,
which is the whole draft-only guard `05` leans on for uploads.

### 5.1 `expectedRecipientCount`

Removing the submit step removed the moment where two people editing one campaign discovered each
other. The launch body carries the count the launcher believes they are launching:

```jsonc
POST /v1/convocatorias/campaigns/{id}/launch
{ "expectedRecipientCount": 227 }
```

A mismatch is `409 CAMPAIGN_RECIPIENTS_CHANGED`, carrying the actual count so the screen can say
"this was 227 when you opened it and is 240 now". The launcher re-reads and launches again. Nothing
is mailed and nothing is written.

The field is **mandatory**, so an omitted count is a `400` rather than a launch that skips the check.
An optional field here fails open on exactly the client that most needs the guard: one written
quickly against Scalar, which is how every launch happens until Roberto builds a screen.

A row version or an `If-Match` header was rejected. The thing that changes is not the campaign row,
it is the count of rows in a different table, so a version stamp on `campaign` would have to be
bumped by every recipient write to mean anything, and a coordinator reading a screen understands
"227 became 240" in a way they never understand an opaque ETag. The cost accepted is that the check
is on the count and not on the membership, so two simultaneous edits that add one person and remove
another slip through. That is a narrower hole than the one it closes, and both people are looking at
the same list.

## 6. Building the recipient list from CRM

`GET /v1/contacts?classification=&companyCategory=&page=&pageSize=` on crm-service, through
`AddServiceClient`. Repeatable filters, **OR within a dimension and AND across them**.
`pageSize` is capped at 100, so 800 contacts is eight calls paid **once**, at selection.

Manual one-at-a-time search was rejected (it does not scale to a roadshow blast) and so was a
saved-segments entity (a CRM feature nobody asked for). Classifications already exist.

**The list is materialised into `campaign_recipient` rows at selection time, never stored as a live
query.** A campaign is history.

**Or by explicit contact ids** (PGTI-179): `{ "contactIds": [...] }`, 1 to 1,000 per call, with
neither filter dimension beside it (an empty list or a mix is `400`), for campaigns with or without
an event. It reads `GET /v1/contacts?id=&id=`, 100 ids per call, and excludes exactly as the filter
does. An id CRM does not return (unknown, soft-deleted, erased) writes no row and is not counted in
`matched`; selection stays additive, so a longer list is several calls.

**Or by a CRM contact list** (PGTI-173): `{ "contactListId": "..." }`, alone like `contactIds`.
Convocatorias reads `GET /v1/contact-lists/{id}` with its own service token (`crm:service-read`,
which sees every OPT's lists), then `GET /v1/opts/{optOfficeId}`, and compares that office's `code`
with the caller's `opt_code` claim. A caller with no `opt_code` (central_admin) or whose OPT is closed
is `403 OPT_NOT_ASSIGNED`, before any CRM call in the first case; a list that does not exist or
belongs to another OPT is `404 CONTACT_LIST_NOT_FOUND`. The members then go through the `contactIds`
path, so the exclusions are the same. Only the resulting recipients are stored, never the list id.

**Nothing is filtered out of the CRM read.** A contact with no email comes back, and so does a
suppressed one, each carrying `canBeEmailed` and a `suppressionReason` code. Convocatorias excludes
them at materialisation and shows them as excluded-and-why. A user who expects 240 recipients and
silently receives 227 has no way to find the missing 13.

`companies: [{ id, name }]` comes back for display at selection and is **never snapshotted onto the
recipient row**. A company name is not what was mailed.

### 6.1 Exclusions

Every contact filtered at selection gets a `campaign_exclusion` row with its reason code.

- Not counters on `campaign`, because the question is "I selected 240 and see 227, who is missing",
  and a number cannot answer it.
- Not a fourth `campaign_recipient` state, so "recipient" keeps meaning "someone we intended to mail"
  and every count over that table stays honest.

**Handoff-time suppression writes the `campaign_recipient` row only, never an exclusion row.** One
place per person per campaign, or every count double-reports.

The four reason codes are `no_email`, `opted_out`, `do_not_invite` (all computed by CRM, first match
wins in that order) and `contact_unavailable` (computed here, `04` §3). Convocatorias renders its own
Spanish per code; the `message` is never a contract.

### 6.2 Degradation

| Call | On CRM unavailable |
| ---- | ------------------ |
| Recipient selection | **Fails**, `503 CRM_UNAVAILABLE`. A list cannot be built from nothing |
| Contact creation (`05`) | **Fails**, `503 CRM_UNAVAILABLE`. The batch stays staged and the user retries |
| Reading a sent campaign | **Never calls CRM** |

This departs from the root `CLAUDE.md` resolve-at-read-time rule on purpose, and ADR-0017 records why
so nobody reuses the ADR-0013 ladder here by reflex.

## 7. Endpoints

All under `v{version:apiVersion}/convocatorias/`. Reads need `convocatorias:read`; the writes are
marked.

| Route | Role | Notes |
| ----- | ---- | ----- |
| `GET /campaigns` | `convocatorias:read` | paged, filters `eventId` and `state`. Not the per-event rollup, which is `07` §2 |
| `POST /campaigns` | `campaigns:write` | creates a `draft` for one event, template key and language taken from the event. The pair must have a template, §4.1. With no `eventId` it is a direct message, §1.1 |
| `GET /campaigns/{id}` | `convocatorias:read` | |
| `PATCH /campaigns/{id}` | `campaigns:write` | draft only. The resulting pair must have a template, §4.1. `subject` and `message` only on a direct message, §1.1 |
| `POST /campaigns/{id}/recipients/select` | `campaigns:write` | §6, draft only |
| `DELETE /campaigns/{id}/recipients/{recipientId}` | `campaigns:write` | draft only |
| `POST /campaigns/{id}/launch` | `campaigns:launch` | `draft` to `sending`. Body `{ expectedRecipientCount }`. Returns `202`, `04` §1 |
| `POST /campaigns/{id}/cancel` | `campaigns:write` | from `draft` or `sending` only |
| `POST /campaigns/{id}/resume-handoff` | `campaigns:write` | `sending` only. Re-posts the `pending` rows, `04` §5 |
| `POST /campaigns/{id}/retry-failed` | `campaigns:write` | `sending` or `sent`. Re-posts the `failed` rows, `04` §5 |

`submit`, `withdraw`, `approve` and `reject` do not exist. Nothing replaced them one-for-one: the
four collapsed into `launch`.

**Two roles, not one.** `campaigns:write` builds; `campaigns:launch` sends it out. Both sit on all
three composites today (`00` §2), so nobody is narrowed on day one, and the split exists so that
narrowing later is a console change with zero code behind it. Merging them now would make that a
code change instead, which is the exact trade ADR-0010's fine-grained/composite split exists to
avoid.

**`retry-failed` is the one action a `sent` campaign still accepts**, and that is its normal case:
what closes a campaign is having no `pending` row left, so a campaign carrying rejections is already
`sent` by the time anybody looks. It does not reopen the campaign — `sent` stays terminal and the §2
table gains no edge — it puts those rows back to `pending` and the worker closes the campaign again
with `sent_at` brought up to date. From `draft` or `cancelled` both routes answer
`409 CAMPAIGN_NOT_SENDING`.

**`resume-handoff` and `retry-failed` take `campaigns:write`, not `campaigns:launch`.** They mail the
tail of a list somebody already launched, so they are unsticking rather than deciding, which is the
same reasoning `07` §6 uses for the reconciliation actions. They are the only way out of a stranded
send, since nothing in this service retries on its own.

## Definition of done

- Every transition in §2 is covered by a test, and every transition **not** in the table is refused
  with a machine code. `cancelled` from `sending` leaves untouched rows `pending`.
- A campaign in `sending` refuses a recipient change, a template change and a language change.
- A launch with no recipient rows is `400 CAMPAIGN_HAS_NO_RECIPIENTS`; a launch whose every recipient
  is unmailable is `400 CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS`. Both leave the campaign in `draft` with
  `launched_by` and `launched_at` still null, and mail nobody.
- Create, PATCH and launch each refuse a `(template_key, language)` pair with no template with
  `400 CAMPAIGN_TEMPLATE_NOT_FOUND`, naming both. Create writes nothing, PATCH leaves the row as it
  was, and launch leaves the campaign in `draft` without calling CRM.
- A launch carrying a stale `expectedRecipientCount` is `409 CAMPAIGN_RECIPIENTS_CHANGED`, changes no
  state, and the response carries the actual count. A launch omitting the field is a `400`.
- A successful launch stamps `launched_by` and `launched_at`, leaves `sent_at` null, and returns
  `202` (`04` §1).
- No `campaign_approval` table, no `ICampaignApprovalGate` and no self-decision guard exist anywhere
  in the solution. Assert it as an absence in the migration set, not only in prose.
- Selection against a CRM stub returning a mix of mailable, emailless and suppressed contacts
  materialises only the mailable ones and writes one `campaign_exclusion` per excluded contact, with
  counts that add up to what CRM returned.
- Selection returns `503 CRM_UNAVAILABLE` when the CRM stub is down, and creates no partial list.
- A caller holding `campaigns:write` but not `campaigns:launch` gets `403` on launch and `200` on
  every draft edit.
