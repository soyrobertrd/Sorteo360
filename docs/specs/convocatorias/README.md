# Convocatorias — Build Plan Overview

Plain-language overview of the convocatorias effort. This page is the index; the numbered specs
beside it hold the detail, and `BUILD-ORDER.md` is the queue to work them in.

**Planning is complete.** Every decision below came out of the Convocatorias Wayfinding map
(2026-08-31 to 2026-09-04), whose closed tickets are the reasoning behind each line here. The
boundary decisions are `docs/adr/0017`; the launch handoff is `docs/adr/0018`. Two client answers are
still outstanding and neither blocks the build; see "What the client still owes us".

**Amended 2026-09-07: there is no approval step.** The client reversed it after the map closed. What
that changed and what putting it back would cost are on the closed ticket "Define the campaign state
machine and the deferred approval seam"; the specs below are already rewritten around it.

Last updated: 2026-09-07.

## What we are building

Inviting people to a MITUR event and tracking what became of each invitation. Three halves:

1. **Campaigns** — pick a template, build a recipient list from CRM or a spreadsheet, launch it, hand
   it to a sender, and record what happened per person.
2. **The registration portal** — a public, anonymous surface where an invitee clicks a tokenised
   link, fills a form, and gets a reference code and a QR.
3. **The tracking read** — who we invited, who registered, who did not, and the short list of
   things a human has to finish by hand.

The service's own vocabulary is `convocatorias-service/GLOSSARY.md`. Read it before the specs: it
fixes what a Campaign, a CampaignRecipient, a Registration and a Participant each mean, and which
Spanish word each English identifier renders as.

## Decisions already locked

- **The service stays standalone**, because the registration portal is anonymous internet-facing
  traffic and events-service is authenticated on every endpoint. ADR-0017.
- **A convocatoria has a list and a per-recipient outcome. Everything else is a notification.**
- **Convocatorias owns its templates**; notification-service is a sending gateway that receives a
  body plus a small data bag per recipient and does the substitution.
- **The service ships before notification-service exists**, against a stub selected by
  `Notifications__Transport`.
- **Suppression fails, it does not degrade.** Convocatorias refuses to launch a campaign it cannot
  suppression-check. Degrade when the missing data is display text, fail when it is permission.
- **Three entities.** `campaign` per send, `campaign_recipient` per contact per campaign,
  `registration` per person per **event** with a nullable link to the recipient whose token brought
  them. That nullable link is what makes a forwarded invitation representable.
- **Ids plus a send-time snapshot, both.** `contact_id` for identity, name and address as actually
  used at handoff, because a campaign is history.
- **One token per invitee, two policies.** A private event's token admits exactly one registration
  bound to that invitee; a public event's admits many, each recording whose link it came through.
- **Nobody approves a campaign.** Whoever holds `campaigns:launch` launches it, in one call. Four
  states, `draft` to `sending` to `sent` plus `cancelled`, and no resting state in which a campaign
  waits for a person.
- **A launch returns `202` and the batches run on a worker.** The request validates, flushes,
  re-checks and flips to `sending`; an in-process `BackgroundService` does the mailing, so closing
  the laptop does not stop it. ADR-0018.
- **A campaign that would mail nobody is refused**, not recorded as sent to zero people.
- **Uploads stage, then a person confirms.** Only on confirmation does convocatorias create the
  unmatched contacts in CRM.
- **Self-registration creates a CRM contact immediately** with consent recorded from the
  registration itself, and **never overwrites** an existing one.
- **Nothing retries on its own.** Every stranded write is resumed by a person from the
  reconciliation screen.
- **A person shuts registration, and nothing else does.** A `registration_window` row in convocatorias
  keyed by event id, null meaning open, two endpoints under `campaigns:write`, and a distinct
  `409 REGISTRATIONS_CLOSED` rather than cancellation's shared `404`, because the event is still
  listed and still serving its detail page. No capacity number closes it.
- **`Event.InvitedCount` stays manual.** It is the OPT's planned target; convocatorias reports
  actuals separately, and events-service never reads them.

## The specs

| Spec | What it covers | Whose code |
| ---- | -------------- | ---------- |
| `00-foundation.md` | Authentication, the realm, route prefixes, nginx, rate limiting, config, test infrastructure | convocatorias |
| `01-events-contract.md` | `visibility` and `language` on `Event`, the typed events client, the public event view | events + convocatorias |
| `02-templates.md` | The `template` table, seeding, the placeholder rules, the substitution engine | convocatorias |
| `03-campaigns.md` | Campaign and recipient state machines, launch, CRM recipient selection, exclusions | convocatorias |
| `04-send-and-handoff.md` | `ICampaignSendGateway`, the amended notification contract, the stub, the launch path and its worker | convocatorias |
| `05-recipient-uploads.md` | Spreadsheet staging, the six buckets, the confirmation write | convocatorias |
| `06-registration-portal.md` | The six public endpoints, tokens, QR, unsubscribe | convocatorias |
| `07-tracking-and-reconciliation.md` | The rollup, the participant list, the CSV, the four reconciliation buckets | convocatorias |
| `08-crm-ask.md` | Everything crm-service must build first | **Manuel** |
| `09-check-in.md` | The CheckIn at the door, the door list, the raffle's check-in list, `event_staff` | convocatorias + events + realm |
| `10-scanner-terminal.md` | Recognising a code, the prize path, the door Event list, two terminals at once | frontend + events |

`08` is the one with lead time and no dependency on us. Send it first.

## Out of scope, and why

Ruled out during charting rather than forgotten. Anything here that arrives as a request is a new
ticket, not a gap in these specs.

- **Opens, clicks, delivery and bounce rates.** They need the provider webhook, which is blocked on
  the client's provider choice. The tracking responses carry no such field at all, so nothing can
  render a null as a zero.
- **Reminders and resends to non-registrants.** A recipient list derived from another campaign's
  outcomes, which the materialise-at-selection rule does not cover.
- **Campaign scheduling.** Nobody asked. It is no longer true that it would be the platform's first
  background execution, since ADR-0018 lands one, but a scheduler is a different thing: it needs
  durable state, because a campaign scheduled for Tuesday has to survive Monday's redeploy and the
  ADR-0018 worker deliberately does not.
- **Staff-managed templates.** The table ships now precisely so the endpoints are additive.
- **Per-campaign arte, and file storage of any kind.** Artwork is an image URL inside the template
  body.
- **Per-OPT row scoping via `opt_code`.** The claim is live and no `.cs` file reads it. Scoping the
  campaign list while the event list beneath it stays open is worse than either answer alone; it
  moves the day events-service answers the same question.
- **Automatic closure of registration at a capacity number.** The manual door shipped instead; the
  limit needs a venue layout chosen, a rule for a multi-city roadshow, a transaction for the race at
  N-1, and an answer on reopening past the cap. It is a backlog card, not a gap.
- **Capacity limits on registration**, a resubscribe flow, RFC 8058 one-click unsubscribe, images on
  the public event page, and fuzzy company matching.
- **The mock's inventions**: `WAITLIST`, scoring, per-segment capacity, the email and domain
  allowlist, the drag-and-drop template block builder, and the twelve extra campaign types. None has
  a client requirement behind it, and the block builder is the opposite of what the client asked for.

**No longer out of scope, as of 2026-09-20:**
- **The check-in scanner.** This service generates the QR and now also validates it at the door:
  attendance is what qualifies someone for a raffle, so check-in became load-bearing. The endpoint
  lands here because this service owns registrations. Specified in `09-check-in.md` (PGTI-39); the
  scanner terminal itself in `10-scanner-terminal.md` (PGTI-40).
- **Raffles.** Formerly listed above as a mock invention. They are real, and owned by
  `events-service`, not by this service. See `plans/raffles-and-frontend-wiring.md`.

## Nothing on our side is open

The last one closed on 2026-09-07: **how registration closes to new registrations**. A person with
`campaigns:write` shuts the door, a `registration_window` row records who and when, and nothing is
sent. The event keeps its place in the public list with `registrationOpen: false`, while resolve and
submit answer `409 REGISTRATIONS_CLOSED`, a code deliberately distinct from the cancelled-event `404`
because the detail page is still serving the same event two clicks away. Reopening is one call. The
shape is `06` §5. It came out of the cancellation ticket, where the client called it *"cerrar una
convocatoria"*, and the first act of settling it was taking that name away: `EventStatus.closed`,
`events:close` and `cerrar` already mean the event happened, which is the opposite situation.

Before it, on the same day: **what cancelling an event does to the people who already
registered**. It mails them, and touches nothing else. The client supplied the case that settled it —
two events for one date in different cities, the one with fewer registrations cancelled days or months
later — and the shape is `06` §4.7: a transactional fan-out of a generic `event_cancelled` template to
every registration, `cancellation_notified_at` on the row, and two calls from one dialog rather than
anything automatic, since events-service never calls us. Only `cancelar` sends it; `cerrar` sends
nothing.

It sharpened one line of the charter along the way. The old test made anything with a recipient list
and a per-recipient outcome a campaign, which would have made this one. The test now reads: **a
campaign's list is selected by a person from CRM; a fan-out's list is derived from rows we already
hold.**

## What the client has answered

- **2026-09-07: convocatorias needs no approval.** Anyone holding the right role launches a campaign
  directly. This came from the client as a change of mind, not as a correction of a misreading on our
  side, which is why the wayfinding ticket that decided the opposite is amended rather than marked
  wrong. It cost the `campaign_approval` table, `ICampaignApprovalGate` and both implementations, the
  self-decision guard, the `campaign_approval_request` template, one campaign state and one realm
  role.

## What the client still owes us

Neither blocks the build. Both change numbers and copy, not shapes.

1. **The upload file format.** A real sample file, the column list, which columns are mandatory,
   whether it carries a company, and the real row count. `05` names 500 rows as a placeholder with
   this ticket as its owner, and its no-email rule is the first thing to revisit.
2. **The existing convocatoria emails**, exported from the system this replaces, in `es`, `en` and
   `fr`. `02` seeds four provisional keys in `es` only. The copy is not ours to invent.

When both land, they get one grilling session together rather than two quiet edits: the row count
re-derives the upload ceiling, and the real template set may cut the keys differently.
