# convocatorias-service: campaigns

> Moved from `convocatorias-service/CLAUDE.md` (PGTI-212), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' convocatorias-service/docs/*.md` finds it.
> Spec numbers (`03`, `04` §1, ...) are files in `docs/specs/convocatorias/`.

## The template catalogue

One whole email — subject and body — per `(templateKey, language)`, in a `template` table with
six columns and nothing else (`docs/specs/convocatorias/02-templates.md`). Five `es` rows ship
today, and there is **no create, update or delete in phase 1**. Four rules are worth knowing
before touching it:

- **Seeding is `migrationBuilder.InsertData`, never EF `HasData`.** `HasData` lives in the model
  and re-asserts its values on every migration generated afterwards, so it would silently revert a
  template edited through the staff-managed endpoints the client has already asked for.
  `Tests.Unit/Database/TemplateSeedIsNotModelManagedTests` holds the proof twice over: the
  design-time model carries no seed data at all, and EF's own model differ — the thing that decides
  what the *next* migration emits — comes back empty. Both fail if anyone reaches for `HasData`.
  A row added after `AddTemplates` is the one exception to `InsertData`: that migration reads
  `TemplateSeed` live, so a fresh database already has the row, and the new migration inserts it with
  `ON CONFLICT DO NOTHING` through `migrationBuilder.Sql` (`AddDirectMessage` is the example).
- **`Core/Templates/TemplateSeed.cs` is frozen.** It is the content of an applied migration, so
  editing it would change what a fresh database gets without touching an existing one — the same
  divergence `HasData` was rejected for. A copy fix ships as a new migration with its own values.
  `TemplateSeedTests.Rows_ReturnsTheFrozenFingerprint_WhenHashed` pins a hash of the seeded rows so an edit fails the
  build instead of drifting quietly; update the hash in the same PR as the migration.
- **Placeholders are derived, never entered.** The supported set is code
  (`Core/Rendering/SupportedPlaceholder.cs`); the used set is scanned out of the body. Every
  placeholder a body names must be in the supported set, a `campaign` body must carry
  `{{enlace_baja}}`, and a `transactional` one must not — all three charged by unit tests over the
  seeded rows, so a typo fails the build instead of reaching a thousand inboxes.
- **A campaign may only name a pair that exists.** Create, PATCH and launch each refuse a
  `(templateKey, language)` pair with no row with `400 CAMPAIGN_TEMPLATE_NOT_FOUND` (spec 03 §4.1),
  using the same exact match the worker renders with. So with only `es` seeded, an `en` campaign is
  refused up front instead of launching with `202` and stranding in `sending`.

**Substitution is flat, literal and HTML-escaped**, with no conditionals, loops, helpers or raw
escape hatch. That is a contract and not an implementation detail: the body crosses a service
boundary and notification-service is what substitutes it on the send path
(`docs/specs/convocatorias/04-send-and-handoff.md` §2), so both sides speak the same dialect on
purpose and each keeps its own copy of the scanner.

## The campaign model

Three tables and no fourth (`docs/specs/convocatorias/03-campaigns.md`). `campaign` is one send;
`campaign_recipient` is one person it means to mail, id **plus** the name and address snapshot;
`campaign_exclusion` is one person the selection filtered out, with a reason code. Both child
tables are unique on `(campaign_id, contact_id)`, and a person is on a campaign as one **or** the
other and never both — that pair of constraints is what keeps every count over the two tables
honest.

Four things are worth knowing before touching it:

- **Nobody approves a campaign.** The client removed the approval step on 2026-09-07
  (`docs/adr/0017`). There is no `campaign_approval` table, no `ICampaignApprovalGate`, no
  `pending_approval` state and no `campaigns:submit` / `campaigns:approve` role. The four acts —
  submit, withdraw, approve, reject — collapsed into one, `launch`, with `campaigns:launch`.
  `MigrationsTests` and `AuthorizationTests` both assert those absences, so re-adding one fails
  the build rather than quietly reappearing.
- **The two state machines never derive from each other.** `CampaignState` is the campaign's
  position (`draft → sending → sent | cancelled`); `RecipientSendStatus` is one row's handoff
  (`pending → handed_off | failed | suppressed`). `registered`, `opened`, `clicked` and
  `delivered` are deliberately off the second axis — folding them in is where the mock's
  `WAITLIST` and its scoring came from. The transition table lives beside the catalogue in
  `CampaignState`, not spread across the repository.
- **The state guards run inside the writing transaction**, in `CampaignRepository`, never in the
  endpoint. Reading a campaign's state first and deciding after would let two simultaneous
  launches both pass a draft that stopped being one mid-flight.
- **`launched_by` / `launched_at` are the launch audit.** No history table beside the state
  column, because one row per campaign cannot disagree with itself. `sent_at` is separate and is
  stamped later, by the worker step 5 brings, never inside the launch request.

**Launch runs a fixed order, and the order is the feature** (`04` §1). `CampaignLaunchService`
owns it, because its steps interleave with CRM and cannot sit in one transaction:

1. **Validate**, from `ReadLaunchPreflightAsync`: the campaign exists, it is a draft, it has rows,
   and the count matches. This runs **before** CRM is touched on purpose — a stale count writes
   nothing (`03` §5.1), and a flush ahead of it would write its stamps anyway. It is a guard read,
   not the authority: the same checks run again inside the writing transaction, which is where
   they have to run so two simultaneous launches cannot both pass one draft.
2. **Flush every unsynced unsubscribe** to `POST /v1/contacts/{id}/opt-out` (`04` §3.1). It is
   **service-wide, not per campaign** — somebody who unsubscribed in March has to be suppressed in
   April, and that row lives under another campaign. The stamps go in **one write, only if the
   whole flush landed**: CRM's opt-out is idempotent, so replaying a landed push is free, while a
   stamp for a push that never landed is the failure the ordering exists to prevent.
   **CRM's `404` is not a failure**: that contact is soft-deleted, so nobody can mail them and the
   unsubscribe counts as landed. Refusing there would be far worse than it looks — the flush is
   service-wide and stamps nothing it did not land, so one dead row would fail *every* launch in
   the service, forever.
3. **Re-check the whole recipient list** against `POST /v1/contacts/mailability`, chunked at 1,000
   (`04` §3). A `canBeEmailed: false` lands that row on `send_status = suppressed` with CRM's own
   reason; **per-recipient suppression is not an error and carries no code**, and the launch
   proceeds for the rest. An id CRM does not return is `contact_unavailable`, computed here because
   CRM cannot report on a row it will not return.
4. **Launch**, inside the transaction: the state guard, the counts again, the suppressions, the flip.

**CRM unreachable at either step is `503 CRM_UNAVAILABLE` and hands off nobody** — the campaign
stays `draft` and no recipient row moves. Half a campaign mailed against an unverified suppression
list is worse than none. **Zero mailable after the re-check refuses the launch** with
`CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS`, and the count runs *before* anything is written, so a
refusal never leaves a suppressed row under a campaign that is still a draft.

Flushing first is also what keeps **a second suppression rule out of this service**: rather than
teaching it to reason over its own `unsubscribed_at`, it makes CRM's answer correct before asking
the question. The local `unsubscribed_at` check inside `LaunchAsync` was never removed; it adds to
the re-check rather than replacing it.

**Recipient selection reads CRM and refuses to degrade.** `GET /v1/contacts` through the chassis
`AddServiceClient` (`Services__Crm__BaseUrl`), paging at 100. CRM filters nothing out of that
read — emailless and suppressed contacts come back carrying `canBeEmailed` and
`suppressionReason` — and this service excludes them *visibly*, one `campaign_exclusion` row
each. With CRM down the whole selection is `503 CRM_UNAVAILABLE` and writes nothing: a list
cannot be built from nothing. That is a deliberate departure from the root `CLAUDE.md`
resolve-at-read-time rule, recorded in `docs/adr/0017` so nobody reaches for the ADR-0013 ladder
here by reflex. **The suppression rule itself is never copied into this service**: CRM computes
`canBeEmailed` and we store whatever reason code it sends, falling back to `contact_unavailable`
for one we do not know.

Selection is **additive and idempotent**: a contact already a recipient counts as `skipped` and
is not rewritten, so two selections compose instead of duplicating and a second one never
silently drops a list someone curated by hand. **An exclusion is not permanent**, though — if CRM
now says a previously-excluded contact is mailable, the exclusion row is deleted and they are
materialised as a recipient. Without that, and with no route that un-excludes, a contact excluded
once could never enter that campaign again.

**The CRM pager fails rather than truncates.** An empty body, or hitting the page ceiling with a
still-full page, is `503 CRM_UNAVAILABLE` — not a short list returned as if it were whole. A
partial list nobody can tell from a complete one is the same invisible failure §6 exists to
prevent.

**That pager is not this directory's own.** It is `Infrastructure/Neighbours/NeighbourPageReader`,
shared with `EventDirectory`, and it is the only place in this service where a neighbour's pages are
walked: the page size, the ceiling and the empty-body branch live there once. The two `catch` blocks
live one level down, in `Infrastructure/Neighbours/NeighbourCall`, which every directory call goes
through, paged or not, and so do both notification-service gateways. It owns the transport
translation and nothing else: what a null body, a 409 or a gone contact means stays with each
caller. Written twice they were applied twice differently — the cancellation guard was missed on the
events copy and only caught in review — so a third neighbour inherits these decisions instead of
retaking them. The reader decides **when** to fail; the caller passes **what** to say, which is why
`CRM_UNAVAILABLE` and `EVENTS_UNAVAILABLE` are still two errors. A new paged read calls it rather
than copying the loop.

## The direct message

A campaign may have no event (ADR-0026, PGTI-174): a direct message to one or more contacts, with
no registration link and nothing to register for. It is still a campaign, so the unsubscribe flush,
the mailability re-check and every suppression rule apply unchanged.

- **One rule, in `Core/Campaigns/CampaignDirectMessage`**: no event **if and only if** the template
  is `direct_message`, and then `subject` and `message` are set; an event campaign carries neither.
  Create and PATCH check it with the resolved values and refuse with
  `400 CAMPAIGN_DIRECT_MESSAGE_MISMATCH`; `ck_campaign_direct_message_is_whole` holds it in the table.
- **Launch skips steps 4 and 5** (the event read and the registration window) and stamps no event
  values. The worker puts `asunto` and `mensaje` in `sharedData` instead, mints only the unsubscribe
  token, and `about` carries the campaign alone. With no registration hash, a recipient has no link
  to register with, and nothing reaches `FindInvitationAsync`.
- **`direct_message` is a seeded `campaign` template, not selectable**, `es` only. The message is
  plain text, HTML-escaped like any value; the body wraps it in `white-space:pre-line` so its line
  breaks survive. `asunto` and `mensaje` are the `Campaign` placeholder grain. It shipped the
  frozen-copy way: `TemplateSeed` row, fingerprint, and `AddDirectMessage`, whose insert is
  `ON CONFLICT DO NOTHING` because `AddTemplates` already reads `TemplateSeed` live on a fresh database.
- Its recipients are read by campaign id, `GET .../campaigns/{campaignId}/recipients` (PGTI-178), since
  there is no event to read them through; see "The participants list".
- **Recipients can be chosen by explicit contact ids** (PGTI-179), for any campaign:
  `contactIds` on the select body, 1 to 1,000, never beside a filter. `ListContactsByIdAsync`
  dedupes and reads `GET /v1/contacts?id=` in batches of 100 (CRM's cap), and the result goes
  through the same materialisation, so the exclusions are identical. An id CRM does not return
  writes no row and only shows as `matched` below the distinct ids sent (repeats are dropped
  first, so compare against the deduplicated list).
- **Or by a CRM contact list of the caller's OPT** (PGTI-173): `contactListId`, alone. The list
  is read with this service's own token, which holds `crm:service-read` and so sees **every** OPT's
  lists: the OPT rule is this service's to enforce, in `Core/Crm/ContactListReader`. It compares
  the list's office `code` (`GET /v1/opts/{optOfficeId}`, read live, never stored) with the caller's
  `opt_code` claim (`ICurrentUser.OptCode`). No claim is `403 OPT_NOT_ASSIGNED` without asking the
  CRM, a closed office the same, and another OPT's list `404 CONTACT_LIST_NOT_FOUND`, like a missing
  one. The members then take the `contactIds` path. CRM down is `503`, as for every selection.

## Recipient uploads

A coordinator drops a spreadsheet on a draft campaign; it is parsed and **discarded**, the review
screen shows what will happen to every row, and only on confirmation are the unmatched contacts
created in CRM (`docs/specs/convocatorias/05-recipient-uploads.md`). Two tables,
`campaign_upload_batch` and `campaign_upload_row`, and two routes under the campaign,
`POST .../recipient-uploads` and `.../recipient-uploads/confirm`, both `campaigns:write` and both
behind the same draft-only guard that makes a `sending` campaign read-only.

Six things are worth knowing before touching it:

- **The column mapping is provisional and lives in exactly one file**,
  `Core/Uploads/UploadColumnMap.cs`. The client's real sample file has not arrived, so first name,
  last name, email, phone and company are a placeholder: expect to change it **once**, there, rather
  than to design for change. The matching forgives case, accents and separators, which is why the
  list is short.
- **A sheet's phone reaches the CRM only in the CRM's shape** (PGTI-355). `CrmContactDirectory`
  drops one that breaks `Core/Shared/PhoneShape` and creates the contact without it: nobody can fix
  a sheet from the review screen, and the CRM's 400 would otherwise fail the row on every retry.
- **The batch row is permanent and its rows are not.** Confirmation deletes the rows — that is where
  the bulk is — and keeps seven integers plus who confirmed. It is permanent because `08` §4 defers
  `onBehalfOfUserId` on `POST /v1/contacts`, so every uploaded contact is attributed to the service
  account, and that deferral is only acceptable because this row records who confirmed. The partial
  index `WHERE confirmed_at IS NULL` is what makes a second upload **replace** the pending one while
  leaving every confirmed one alone; cancelling a draft takes its pending batch with it, which is why
  there is no TTL and no cleanup job.
- **Six buckets, evaluated in order, first match wins**, and they reconcile:
  `data rows = matched + unmatched + suppressed + already_a_recipient + duplicate_in_file + malformed
  + collapsed_exact_duplicates`. A database CHECK holds that equation. `already_a_recipient`
  deliberately outranks `suppressed`, and **a row-level failure never blocks an upload** — a file
  where every row is malformed stages successfully with zero confirmable rows, because that is how
  somebody discovers their columns are wrong.
- **A row needs both name columns, not one.** crm-service's `CreateContactRequestValidator`
  requires `firstName` and `lastName`, so a half-named row could only be confirmed by inventing the
  missing half — and writing a `(sin apellido)` into the system of record is the junk the review step
  exists to prevent. A row missing either is `malformed`/`missing_name`, which means no placeholder
  name ever reaches CRM. Like the column map, this is provisional: a client file with one
  full-name column would make every row malformed, and the fix is `UploadColumnMap`, not this rule.
- **CRM normalises and we do not.** Convocatorias trims and sends the raw address to
  `GET /v1/contacts?email=`; the only local key is in-file dedupe, and it is trim-plus-lowercase
  only, a conservative subset, so drift makes us *under*-merge into a `409`, which is already a
  defined non-failure. No gmail dot-stripping and no `+tag` handling anywhere.
- **Confirmation is synchronous, resumable and idempotent per row.** The batch is its own cursor
  (`applied_at`), a failure stops the loop and answers `503 CRM_UNAVAILABLE` with `completed` in the
  ProblemDetails, and a retry skips what is done. A `409` from CRM is a **match**, not an error —
  take the id, **re-check `canBeEmailed` on the returned body**, and the row moves to `matched` or to
  `suppressed`. A partial confirmation is never rolled back: CRM is the system of record.

The chassis circuit breaker is load-bearing here rather than incidental: it already translates
`BrokenCircuitException` into `HttpRequestException`, so a broken circuit reaches the user as the
same `503` instead of a `500`. `RecipientUploadCircuitTests` lives in its own class because the
resilience pipeline is a singleton and an open circuit would otherwise leak into its neighbours.

**The three limits are code, not configuration** (`Core/Uploads/SpreadsheetUploadLimits.cs`): 5 MB,
500 data rows, 100 MB decompressed. The row cap is a placeholder with a named owner and it is one
decision with nginx's 60s default `proxy_read_timeout` — **raising it means declaring an explicit
`proxy_read_timeout` on the confirm route in `frontend/nginx.conf` and in its uncompared twin
`frontend/vite.config.ts`, or paging confirmation**, not just a bigger number.
