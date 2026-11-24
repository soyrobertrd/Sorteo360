# notification-service: the boundary: no copy, resolution vs. deliverability

> Moved from `notification-service/CLAUDE.md` (PGTI-215), text unchanged except headings. A section named
> with "see ... below" or "§..." may now live in a sibling file in this folder: `grep -n '^## ' notification-service/docs/*.md` finds it.

## Three things that are true of no other service here

### It holds zero copy and zero domain knowledge

No template table. No default body. No notion of an OPT, an evento or a cotización. No hard-coded
placeholder names, no branch on `type`, no template lookup. `type` is a **history label** — a
plain string stored verbatim with no foreign key — so rows about a retired type still read
correctly.

The consequence is accepted out loud: **it cannot send an email of its own**, not even to tell an
operator that a batch failed. A failure is discovered by looking, not by being told.

**If a change here seems to need copy, the design has drifted.** Check ADR-0020 before writing it.

### The resolution / deliverability seam

Two questions, two owners, and they never swap:

- ***Who is this person and what is their address?*** is **resolution**, and it belongs to the
  **caller** — the only party that knows what a role or a campaign means.
- ***Can this address receive mail at all?*** is **deliverability**, and it belongs **here**,
  because the answer arrives on this service's webhook and reaches nobody else.

The pipe never decides who *should* be told. It refuses only addresses the provider has reported
dead or hostile, and records the refusal as a history row rather than dropping it.

A second consequence: **the pipe must not be able to tell an internal recipient from an external
one.** Both arrive as `{ email, name?, data? }`. Every difference — address of record, the consent
gate, the unsubscribe link — lives in the caller. See `GLOSSARY.md`.

## Two rules worth repeating in code review

- **The `202` is the line.** After it a history row exists; before it nothing does. A malformed
  request is *our* bug and answers `400` having stored nothing. A world failure — SMTP down, a
  bounce, a suppression — answers `202` and is stored with a status.
- **`notifications:send` is never granted to a person.** The endpoint cannot tell a person's token
  from a machine's, so on the fallback policy any logged-in coordinator could send mail as MITUR,
  from MITUR's authenticated domain, with an effect that leaves the platform and cannot be
  retracted. It is granted directly to calling **service accounts** and to no composite, ever.
  `RealmConfigurationTests` asserts the realm file; nothing can assert a live realm, which is why
  the rule is also written in words in ADR-0020 and as a console step in the deploy runbook.
