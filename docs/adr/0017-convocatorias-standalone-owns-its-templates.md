# ADR-0017 — convocatorias-service stays standalone, owns its email templates, and refuses to send when it cannot check suppression

- Status: Accepted
- Date: 2026-09-04
- Amended: 2026-09-07, approval removed (see below)

> **Amendment, 2026-09-07.** The client removed the approval step from convocatorias. A campaign is
> now launched directly by anyone holding `campaigns:launch`, and nobody signs it off. Three things
> in this record are affected and have been edited in place: the boundary rule below no longer counts
> an approval among a convocatoria's parts, the approval-request email that the Context lists as one
> of the four things MITUR calls a convocatoria no longer exists, and the realm roles drop from four
> to three. Everything else stands: the standalone decision, template ownership, the wire shape, the
> suppression rule and the nginx boundary never depended on approval. The full reasoning and what
> re-adding approval would cost are in the Convocatorias Wayfinding ticket "Define the campaign state
> machine and the deferred approval seam". How a launch executes is ADR-0018.

## Context

MITUR invites people to its events by email, and then wants to know what became of each
invitation. Today that runs in the system this platform replaces. The request that opened the
work was *"start the convocatorias backend, and I am debating whether it stays its own service or
merges into events-service"*, which is the question this ADR answers, along with the three
cross-service commitments that fall out of it.

`convocatorias-service` exists as a scaffold: the chassis is wired, the two test projects are
empty, there is no domain model, no migration, and no authentication. `Program.cs` deliberately
does not call `AddKeycloakAuthentication`, which makes it the platform's live example of a service
that protects nothing.

Four facts shaped the decision.

**An invitee registers with no login.** They click a tokenised link in a forwarded email and post a
form. That is anonymous internet-facing traffic, and it is unlike every other endpoint on this
platform, all of which sit behind Keycloak.

**Not everything MITUR calls a convocatoria is one.** The client's notes named four things: a
save-the-date, a register-to-attend invitation, a "venue confirmed, please confirm" message, and
the approval request that goes to the head of events. The first two have a recipient list and a
per-person outcome. The last two have one recipient and nothing to track.

**notification-service does not exist**, and the map that owns it is parked on the client's choice
of email provider. Convocatorias cannot wait for either.

**crm-service owns whether a person may be emailed.** The client gave two hard rules: someone who
opts out is never emailed again, and a contact flagged do-not-invite is excluded. Both are contact
data, so both belong to crm-service under the platform's own ownership rule.

## Decision

**`convocatorias-service` stays standalone.** The deciding argument is the public registration
portal. Merging would put an anonymous, unauthenticated surface inside the service that holds
quotes, budgets and approvals. The coupling is thin in both directions — convocatorias reads an
event's name, dates, cities, OPT, language and visibility, and events-service reads nothing back at
all — and the one live direction is the ADR-0013 pattern that already exists. The scaffold's
plumbing is already paid for, so keeping the service costs near zero while merging is itself work.

**A convocatoria has a recipient list and a per-recipient tracked outcome. Everything else is a
notification.** The save-the-date and the invitation are campaigns. The venue confirmation is a
notification, sent by whichever service owns the trigger. The line is testable against any new email
someone proposes, which is what stops the boundary eroding.

**Convocatorias owns its email templates; notification-service is a sending gateway.** Email copy
about convocatorias is domain, and the root `CLAUDE.md` charters shared plumbing for technical
scaffolding and never for domain. Sending, retry, credentials and history are plumbing. Templates
live in a `template` table in this service's database, seeded by a data migration.

**The wire shape is template plus per-recipient data, never rendered HTML.** Every invitee's mail
carries a unique registration token, so rendering per recipient here would put a thousand distinct
bodies in one request. Convocatorias sends the body once and a small data bag per recipient;
notification-service does the substitution. This also amends a closed decision on the notifications
map: `NotificationTypeDefinition`, the type registry, is not built, because its justification was
*"the service that renders is the service that declares what it needs"* and caller-supplied
templates break that premise.

**Suppression cannot degrade, and the rule generalises:**

> **Degrade when the missing data is display text. Fail when the missing data is permission.**

ADR-0013 lets a consumer render a placeholder when crm-service is unreachable. An OPT name that
will not resolve costs a reader a name. A suppression answer that will not resolve costs a person
their legal right, so convocatorias **refuses to launch a campaign it cannot suppression-check**,
returning `503 CRM_UNAVAILABLE` rather than mailing anyone. Both are ADR-0013 consumers calling the
same service; the ladder applies to exactly one of them. ADR-0013 stays correct for what it was
written for.

**A public unauthenticated surface exists, and the network is what constrains it.** Public routes
live under `v{version:apiVersion}/public/` with no handlers shared with the internal ones. The
service is reachable from the internet only through the frontend's nginx, which forwards by path
prefix, so `convocatorias-service` loses its Traefik router and its public hostname. That is a
network-level guarantee in one file, and it closes the exposure the scaffold already carried:
Scalar and `/openapi/v1.json` are served in every non-Production environment, Staging included. An
in-process test walking `EndpointDataSource` backs it up, asserting that every mapped endpoint is
either under the public prefix or carries an authorization requirement.

**The service claims exactly two nginx locations**, `/v1/public/` and `/v1/convocatorias/`, one
prefix per service rather than crm-service's one per resource. That list is hand-maintained and has
an uncompared second copy in `frontend/vite.config.ts`; every extra prefix is two more chances to
land a request in the wrong service.

**`convocatorias:read` is the platform's first read role.** All fourteen fine-grained realm roles
today are writes or actions, and reads carry no policy anywhere — any authenticated user reads
every event, budget and quote in events-service. The tracking reads hold names and addresses of
people who consented to an invitation and not to a directory, and this service is empty, so failing
closed from day one costs nothing. Retrofitting events-service touches every read endpoint and
every human composite, which is a security decision about that service and earns its own ticket.

**A spreadsheet upload attaches to a draft campaign, never to a free-standing import.** Contacts
created from an upload are unattached and carry no classification, and the only recipient-selection
mechanism filters on classification and company category, so a free-standing import would create
contacts that nothing in the product can find again.

### Rejected

- **Merging into events-service.** Above.
- **Every template in notification-service.** Other services will want their own, and the caller can
  supply the template in the call. The cost accepted is that a copy fix for a transactional email
  becomes a deploy of the calling service.
- **A fourth, public-facing service in front of this one.** Same property as the nginx route, plus a
  container, an image to patch and a second point of failure. Keeping the anonymous surface out of
  events-service was the whole point of staying standalone.
- **A message broker.** None runs anywhere and no ADR proposes one. notification-service returns
  `202`, so our handoff is a handful of fast accepts. The place a broker earns its keep is retry and
  delivery feedback inside notification-service, which is that map's question.
- **Redis for rate-limit counters.** ADR-0007 stands. One container runs, so in-process counters are
  accurate, and ASP.NET's limiter has no distributed provider.

## Consequences

- **convocatorias-service gains authentication in the same PR as its first internal endpoint.** It
  calls `AddKeycloakAuthentication`, sets a fail-closed fallback policy, and registers two realm
  clients that do not exist yet: `convocatorias-api` as the audience and `convocatorias-service` as
  the caller identity, mirroring `events-api` / `events-service`. Per ADR-0016 every realm change
  lands twice, in `infra/keycloak/pgti360-realm.json` and as a console step in the stage runbook.
- **Three new realm roles**: `campaigns:write`, `campaigns:launch` and `convocatorias:read`, each on
  `opt_coordinator`, `opt_director` and `central_admin`. No new persona is invented.
  `campaigns:launch` stays a distinct role although its holders match `campaigns:write` exactly
  today, so narrowing launch to dirección later is a console composite and no code, which is what
  ADR-0010's fine-grained/composite split exists for.
- **`Event` gains `visibility` and a non-nullable `language`**, which is a cross-service contract
  change landing in one PR across events-service, convocatorias-service, the realm and both compose
  files. `PATCH /v1/events/{id}` with `language: null` starts returning `400`; that endpoint is a
  replace and not a patch, and this is the one behaviour change reaching an existing client.
- **crm-service gains work with lead time**, owned by Manuel: a unique `email_normalized` among live
  rows, three consent columns, five suppression columns, `canBeEmailed` and `suppressionReason` on
  the contact reads, a `companyCategory` filter, an exact `email` filter, an idempotent opt-out
  endpoint, and a bulk mailability check. Two CRM specs are amended in this PR:
  `03-contacts-gdpr.md` (email is unique per person, the client's own reversal) and
  `04-read-api.md` (the service-caller consent gate is not built in phase 1, because it would hide
  the contacts convocatorias had just created from convocatorias itself).
- **convocatorias never resolves a sent campaign against CRM.** Names and addresses are snapshotted
  on `campaign_recipient` at handoff. A campaign is history: if someone edits their address next
  March, the record of what was sent in September must not rewrite itself. This is the second
  deliberate departure from ADR-0013's resolve-at-read-time rule.
- **The service ships end to end before notification-service exists.** `ICampaignSendGateway` has
  two implementations selected by a required `Notifications__Transport` value, `stub` or `http`,
  with no environment branching, so stage behaves the way production will. While the transport is
  `stub`, stage holds plaintext registration tokens in `stub_notification` beside their own hashes;
  the exit is pinned rather than remembered, and the migration that lands the real client drops the
  table.
- **convocatorias is not an ADR-0014 consumer.** Campaign artwork is an `https://` image inside the
  template body, uploaded spreadsheets are parsed and discarded, and QR images are generated on
  demand. The service stores no files and needs no named volume.
- **A campaign knows what it handed off, never what was delivered.** No `delivered`, `opened`,
  `clicked` or `bounced` field appears anywhere in the tracking responses — absent, not nullable, so
  no screen can render a zero and read it as an open rate. Delivery feedback arrives as additive
  fields the day the notifications webhook question closes.
- **Nothing in this service retries on its own.** A stranded campaign, a registration whose CRM
  contact never landed and an unsynced unsubscribe are all resumed by a person through the
  reconciliation surface. The platform has no background jobs and this feature does not add the
  first one.
