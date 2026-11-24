# What crm-service Must Build — Spec

Everything convocatorias needs from crm-service, in dependency order. **This is crm-service work
only**, owned by Manuel, and it is the item with lead time. Nothing here depends on convocatorias
existing, so it can start immediately and in parallel.

Verified against `origin/crm-new-features` on 2026-09-04.

## 0. What the branch already has

More than earlier drafts of this plan assumed. `GET /v1/contacts` and `POST /v1/contacts` are built:

| Surface | Today |
| ------- | ----- |
| `GET /v1/contacts` | Fallback policy, any authenticated caller. Filters `search`, `classification` (by `Code`), `page`, `pageSize` clamped to 100. Returns `{ items, totalCount, page, pageSize }`, each item `{ id, firstName, lastName, name, email, phone, createdAt, updatedAt, classifications[] }` |
| `POST /v1/contacts` | Requires `contacts:write`. Body `{ firstName, lastName, email?, phone? }`, `createdById` stamped from the token's `sub`. Returns `201` with a bare `Guid` |
| `GET /v1/companies/{id}/contacts` | Paged, the reverse direction |
| `contact` indexes | **None on email.** No seeded contact rows anywhere |

A client-credentials token carries `sub`, so `CurrentUserAccessor` resolves for a service caller and
attributes to the service account rather than failing.

Two gaps drive most of what follows. There is **no company-category filter on contacts** — category
lives on `company` via assignment rows and the contact-company link is `CompanyContact` — so half the
recipient-selection rule is unbuildable as things stand. And **`search` is an `ILIKE '%term%'` over
name and email**, which is wrong for an exact-address lookup: `ana@acme.com` is a substring of
`mariana@acme.com`, so matching on `search` would silently attach a registrant to a stranger's record.

## 1. Email is unique per person

**The client reversed a documented decision on 2026-09-03.** `docs/specs/crm/03-contacts-gdpr.md`
rejected their "Regla Dorada" by name, on the grounds that small companies share a mailbox so two
distinct people may hold one address, and settled that a match is a warning rather than a rejection.
The client has reasserted the rule: **one email, one person.**

This is a client decision, not a technical one. It is recorded here so nobody re-argues it from the
old rationale, which is still persuasive and now moot. Phone numbers stay freely shared; nothing keys
on them.

- `email_normalized` (lowercased, trimmed, maintained in code) is **unique among live rows**, via a
  partial index `WHERE deleted_at IS NULL`, mirroring how a closed OPT office frees its `code`.
  Nullable email survives, because Postgres permits many NULLs under a unique index, so a phone-only
  contact is still legal.
- `03` §3's 409-carrying-an-array collapses to a `409` carrying **one** contact, and its second
  resolution, the explicit override that creates a second contact on the same address, is deleted.

**Check stage for duplicate addresses before this ships.** Migrations run at startup in every
environment, so a duplicate row fails the migration and crash-loops crm-service on deploy. Clean them
by hand first: a merge-duplicates data migration is a decision nobody has authorised, and a silent
auto-merge on a compliance-relevant table is worse than a failed deploy.

## 2. The work, in dependency order

1. **`email_normalized` on `contact`**, with the unique partial index of §1.

2. **Three consent columns on `contact`**: `has_consent` (bool, not null), `consent_at` (timestamptz,
   null), `consent_source` (text, null). **No history table, no `erased_at`, no erasure endpoint in
   phase 1.**

   `consent_source` is load-bearing, because the two creating callers hold different evidence:

   | Caller | `has_consent` | `consent_source` | Evidence |
   | ------ | ------------- | ---------------- | -------- |
   | Confirmed upload | `true` | `verbal_upload` | The employee took verbal consent before adding the person |
   | Self-registration | `true` | `self_registration` | The person entered their own details. The strongest record available |

3. **`POST /v1/contacts` accepts the consent fields**, and returns **`409` with the existing
   contact** (id, first name, last name, email, plus its `canBeEmailed` and `suppressionReason`) when
   `email_normalized` collides with a live row.

4. **`GET /v1/contacts` gains four things:**
   - an exact `email` filter on `email_normalized`, **distinct from `search`**, for the §0 substring
     reason;
   - a repeatable `classification` filter and a `companyCategory` filter, **OR within each dimension
     and AND across them**;
   - `companies: [{ id, name }]` on each item, active affiliations only;
   - `canBeEmailed` (bool) and `suppressionReason` (string, null when `canBeEmailed` is true).

   **Nothing is filtered out of this read.** A contact with no email comes back, and so does a
   suppressed one. Convocatorias excludes them visibly, because a user who expects 240 recipients and
   silently receives 227 has no way to find the missing 13. Excluding emailless contacts needs no CRM
   change at all, which is its best property.

5. **Five suppression columns on `contact`**, dedicated and **not a `ContactClassification`**:

   | Column | Type | Notes |
   | ------ | ---- | ----- |
   | `opted_out_at` | timestamptz, null | Set means opted out |
   | `opt_out_source` | text, null | `campaign_unsubscribe` is the only phase-1 value |
   | `do_not_invite_at` | timestamptz, null | Set means do not invite |
   | `do_not_invite_reason` | text, null | Free text. **Never crosses the wire** |
   | `do_not_invite_by_id` | uuid, null | Who flagged them |

   A classification was rejected because it is user-managed: renaming the lookup row, or deleting it
   and its assignments, would erase a legal record through a lookup-table edit. Timestamps rather than
   booleans, because "when did they opt out" is the question that gets asked and a boolean cannot
   answer it.

   Opt-out is the person's own withdrawal and do-not-invite is MITUR's judgement about a person. They
   are reversed by different people, which is the whole operative difference between them, and
   collapsing them into one flag loses it.

6. **`canBeEmailed` and `suppressionReason` computed from those columns**, precedence fixed, first
   match wins so the code is deterministic when two apply:

   | Code | Meaning |
   | ---- | ------- |
   | `no_email` | The contact has no address |
   | `opted_out` | `opted_out_at` is set |
   | `do_not_invite` | `do_not_invite_at` is set |

   **CRM computes the answer and never exports the evidence.** The rule belongs to its owner:
   otherwise "skip if opted out or flagged do-not-invite" lives in two codebases, and the day CRM
   grows a third reason convocatorias keeps mailing those people until someone remembers to redeploy
   us. Emailless is folded in rather than carrying its own flag, or convocatorias writes
   `if (!canBeEmailed || !hasEmail)` and the rule is in two codebases again through a side door.

   `do_not_invite_reason` never crosses the wire: it is MITUR's private note about a person.

   Soft-deleted contacts are not in the set at all. They never come back from a read, which is what
   convocatorias' own `contact_unavailable` code covers.

7. **`POST /v1/contacts/{id}/opt-out`**, body `{ source }`, one phase-1 value `campaign_unsubscribe`.
   - **Idempotent**: a repeat on an already opted-out contact returns `200`, not `409`. People click
     the unsubscribe link twice, and a repeat click that errors makes the portal say something
     alarming for no reason.
   - Sets `opted_out_at` and `opt_out_source`, and **flips `has_consent = false`, leaving `consent_at`
     and `consent_source` untouched** as the record of what we were told at creation.
   - Returns the contact with `canBeEmailed: false` and `suppressionReason: "opted_out"`, so no second
     read is needed.
   - Authorised by `contacts:write`, already in item 10. No new realm role.

   **The implication runs one way only**: opt-out implies `has_consent = false`; `has_consent = false`
   implies nothing and suppresses nobody. `canBeEmailed` reads the suppression columns and **never
   reads `has_consent`**. One test pins that, or the consent gate deleted in item 9 grows back in six
   months as an accident.

8. **Do-not-invite set and clear** on the existing contact PATCH, `contacts:write`, recording
   `do_not_invite_by_id`. **No endpoint clears `opted_out_at`, in phase 1.** A staff member
   accidentally un-opting-out a contact is the compliance failure the column exists to prevent, and
   there is no consent history to record that it happened. A real resubscribe flow needs the history
   table first.

9. **Do not switch on `04-read-api.md` §2's service-caller consent gate.**

   Trace the upload path against it: convocatorias creates 40 unmatched contacts, then reads them back
   to materialise recipients, and the gate hides every one of them. The service would be unable to see
   the contacts it had just created. `04` conflated consent with the operative rule, which is
   **suppression**. `crm:service-read` stays the marker for a system caller and stays inert.

10. **`POST /v1/contacts/mailability`**, body `{ ids: [...] }` capped at 1,000, returning
    `[{ id, canBeEmailed, suppressionReason }]` and nothing else.

    A POST because a 1,000-id query string is not a URL. Convocatorias re-checks the whole recipient
    list at launch, and one call per contact would be 1,000 round trips inside the launch request.
    **An id that does not come back is treated as not mailable by the caller**, which is how a contact
    soft-deleted between selection and launch is kept from being mailed.

11. **Realm**: a `convocatorias-service` confidential client with service accounts enabled, its service
    account holding `crm:service-read` **and `contacts:write`** as directly-assigned realm roles,
    exactly as `service-account-events-service` holds `crm:service-read` today. Per ADR-0016 it lands
    in `infra/keycloak/pgti360-realm.json` **and** as a console step in the stage runbook.

## 3. Amendments to the CRM specs, owned by us

Both land in the ADR-0017 PR, written on our side, not Manuel's.

- **`docs/specs/crm/03-contacts-gdpr.md`** — email becomes unique per §1, with the client's reversal
  recorded so the old rationale is not re-argued; the `409` carries one contact and loses the
  shared-mailbox override; the consent model reduces to the three columns of item 2, with the
  append-only history and the right-to-erasure flow marked deferred rather than build-ready.
- **`docs/specs/crm/04-read-api.md`** — §2's service-caller consent gate is marked not built in phase
  1, with the reason.

## 4. Coming later, worth knowing now

Neither is in this ask. Both are schema changes with a known owner rather than a surprise later.

- **`onBehalfOfUserId` on `POST /v1/contacts`**, honoured only for callers holding a service role and
  stamped into the existing `created_by_id`. Until then every uploaded contact is attributed to
  `service-account-convocatorias-service`. Accepted because convocatorias' staged batch records who
  confirmed, so the audit trail exists on our side of the boundary, and because the change is
  additive — no new column, no migration, no change to any existing caller — and back-fillable from
  that staged record. `05` §2 is why the batch row is permanent.
- **`jobTitle` and `country` on `contact`.** The self-registration form collects seven fields and only
  four reach CRM in phase 1. Both are real contact attributes and belong in CRM in phase 2. `jobTitle`
  is the awkward one: today it lives on the `CompanyContact` link rather than on the contact, which is
  correct when a person holds different titles at two companies and wrong for a person with no company
  at all, which is exactly what self-registration creates. Worth deciding before it is built, not
  while.

## Definition of done

- Two live contacts cannot share a normalised email; a soft-deleted one frees the address.
- `POST /v1/contacts` on a colliding address returns `409` with the existing contact, including
  `canBeEmailed` and `suppressionReason`.
- `GET /v1/contacts?email=ana@acme.com` does not match `mariana@acme.com`.
- `classification=A&classification=B&companyCategory=TourOperator` returns contacts in (A or B) **and**
  that category.
- Every list item carries `companies`, `canBeEmailed` and `suppressionReason`, and no read ever returns
  `do_not_invite_reason`.
- An emailless contact and a suppressed contact both come back from the list, flagged.
- Precedence: a contact with no email **and** an opt-out reports `no_email`.
- `POST /v1/contacts/{id}/opt-out` twice returns `200` both times and flips `has_consent` once, leaving
  `consent_at` and `consent_source` unchanged.
- A contact with `has_consent = false` and no suppression column set is still `canBeEmailed: true`. This
  is the test that stops the consent gate growing back.
- `POST /v1/contacts/mailability` with 1,000 ids answers in one call; 1,001 is a `400`; an id for a
  soft-deleted contact is simply absent from the response.
- A `convocatorias-service` token reaches all of the above and is refused anything needing a role it
  does not hold.
