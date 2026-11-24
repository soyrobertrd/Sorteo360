# ADR-0021 — An opt-out belongs to the address, not to the row, and survives a soft delete

- Status: Accepted
- Date: 2026-09-19

## Context

A person opts out of email. An admin soft-deletes their contact. An admin creates a new contact on
the same address. Today the new contact is fully mailable, with no opt-out and no trace of the old
one.

This is not a defect against the specification. `docs/specs/crm/03-contacts-gdpr.md` §1 specifies
the unique index on the normalised email as partial, `WHERE deleted_at IS NULL`, and gives the
reason: it mirrors how a closed OPT office frees its `code`. Freeing the address is intended
behaviour and stays.

What was never asked is what freeing the address means for **suppression**, and suppression is the
one thing in this model with a legal character rather than an operational one. The specification's
OPT-office analogy implies the opt-out belongs to the row and goes when the row goes. The purpose
of an opt-out implies it belongs to the person. Both readings are defensible from what was written,
which is why this needed deciding rather than inferring.

Two facts bound the decision:

- **Soft delete is reversible and keeps all personal data.** The old row and its opt-out evidence
  are still there to be found. Nothing has been destroyed.
- **Erasure is not built.** `POST /v1/contacts/{id}/erase` does not exist; there is no `erased_at`
  column and no scrub. So nothing today destroys the evidence that a person said no. This is
  tracked as PGTI-10, and shipping it changes the arithmetic below.

## Decision

**Suppression follows the normalised address, not the contact row.**

Creating a contact whose normalised email matches **any** suppressed contact — live or
soft-deleted — inherits that suppression. The create still succeeds; the resulting contact is
simply not mailable.

The suppression reason travels with it. `ContactMailability` already carries `suppressionReason`
across the wire, so a contact that is born unmailable can say why, and a screen can render it. That
mechanism already exists and needs no new field.

## Consequences

- A create costs one additional lookup across soft-deleted rows on the normalised email. The
  partial unique index does not cover deleted rows, so this needs its own index on
  `email_normalized` to avoid a sequential scan as the table grows.
- A contact can be created unmailable for a reason that originated on a different row. This is the
  honest outcome and the reason `suppressionReason` crosses the wire, but it is surprising to
  anybody reading a single row in isolation, so it is pinned by a test rather than left to be
  rediscovered.
- The bulk mailability check and single-contact reads must agree in this case as in every other.
  They read the same rule or they drift.
- A delete-and-re-add no longer launders an opt-out. That was the point.
- Restoring the soft-deleted contact instead of re-adding still works and keeps its opt-out
  unchanged. Nothing about restore changes.
- A contact whose address matches a soft-deleted contact that was **not** suppressed is unaffected.

## Alternatives rejected

**Refuse the create with a 409 naming the suppressed dead row.** Cheapest to reason about, and it
keeps one person as one record by pushing the admin towards restoring rather than re-adding.
Rejected because it turns a soft delete into something close to a permanent block on an address,
which nobody has asked for, and because it makes an operational cleanup mistake unrecoverable
through the ordinary endpoints.

**Accept today's behaviour and document it.** Free, and defensible if the contacts-delete role is
genuinely trusted. Rejected because the trust argument is the wrong shape: the failure is silent,
it looks identical to a legitimate new contact, and it surfaces as a complaint from somebody who
already told us to stop. A rule with a legal character should not depend on nobody making a routine
mistake.

## Relationship to erasure

Erasure, when it ships (PGTI-10), scrubs the address itself. An erased row therefore cannot be
matched by this rule, which is correct: the person asked for their data to be destroyed, and
keeping a matchable copy of their address in order to honour their opt-out would defeat the
erasure. The two rules do not conflict, but the ordering matters and the erasure ticket must not
re-introduce a matchable remnant.

## Scope

This governs contacts in `crm-service`. It does not change the partial unique index, the OPT-office
`code` analogy it was modelled on, or anything about how a soft delete behaves for companies.
