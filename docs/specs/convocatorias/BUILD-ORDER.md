# Convocatorias — Build Order

The work queue for the specs beside this file. Eleven steps in dependency order, each sized to be
finished, tested and reviewed on its own.

Read the specs for the *why* and the shapes; this page is the order to build in and what blocks what.

## Before you start

**Read these, in this order:**

1. `docs/dotnet-service-conventions.md` — the conventions every .NET service here follows, and the
   patterns deliberately **not** used, which is the faster way to avoid a wrong turn: no MediatR or
   CQRS, no AutoMapper, no controllers, no global exception handler, no `IUnitOfWork` layer.
2. `convocatorias-service/CLAUDE.md` — this service's layout and what the chassis already gives you.
   Note its "Growing the service" section: this queue is that section being carried out.
3. `convocatorias-service/GLOSSARY.md` — the domain vocabulary. What a Campaign, a CampaignRecipient,
   a Registration and a Participant each mean, and which Spanish word each English identifier renders
   as. The `_Avoid_` lists apply to identifiers, never to prose.
4. `docs/adr/0017` — the boundary and the three departures from platform patterns.

**Your reference vertical is `Quote` and `ServiceSupplier` in events-service.** `QuoteStatus` is the
shape `03`'s state guards copy and `Core/Files/` is the shape `05`'s upload policy copies.
`QuoteDecision`, `QuoteDecisionOutcome` and `IApprovalGate` are **not** copied: convocatorias has no
approval step, so there is no decision to record. When a spec and that code disagree on
*structure*, the code wins; on *behaviour*, the spec wins.

**Conventions worth knowing before step 1:**

- Errors carry a machine `code` alongside the HTTP status, emitted by the chassis as a ProblemDetails
  extension. Clients branch on the code and render their own Spanish; the `message` is prose for a
  human and is never a contract.
- Domain names are English: types, tables, columns, JSON fields, routes, error codes. Spanish stays in
  comments, log messages, client-facing error text, the email copy and the UI.
- Declaring `.WithValidation<TRequest>()` removes the inferred `200`, so success codes must be
  declared explicitly.
- `DbResult.TryAsync(logger, action, …)` takes the logger as a **leading** parameter.
- Every outbound call goes through the chassis `AddServiceClient`, never a bare `HttpClient`.
- Every environment variable lands in `docker-compose.yml` **and** `docker-compose.dokploy.yml`.

## The queue

```
        ┌─ 0. CRM ask (Manuel, parallel, no dependency on us) ──────────────────┐
        │                                                                       │
1. Foundation ─┬─► 2. Templates ─► 4. Campaigns ─┬─► 5. Launch + worker         │
               │                                  │         │                   │
               └─► 3. Events contract ────────────┘         ▼                   │
                            │                        6. Gateway + resume        │
                            │                              │   │                │
                            │              7. Uploads ◄────┘   │                │
                            └────────────► 8. Portal ◄─────────┘                │
                                              │                                 │
                                              └─► 9. Tracking ◄─────────────────┘
                                                      │
                                                      └─► 10. Frontend catch-up
```

**Renumbered 2026-09-07.** The events contract moved ahead of the campaign model, because the launch
path reads the event and that stopped being a preference. The old step 4 split in two once the `202`
and the worker landed on it. Steps 0, 1 and 2 are where they were.

### 0. Send the CRM ask to Manuel — `08-crm-ask.md`

**Do this first, on day one, before writing any code.** It is crm-service work with no dependency on
convocatorias, and everything from step 4 onward eventually needs it. Steps 1, 2 and 3 need none of
it, so the two tracks run in parallel.

Blocks: 4, 5, 7, 8 at integration time. **Blocked by:** nothing.

### 1. Foundation — `00-foundation.md`

Authentication, the realm roles and two clients, the two route prefixes, the nginx locations,
deleting the Traefik router, `ForwardedHeaders`, the rate limiter, the package pins, the encoding
provider, `MigrateAsync` on startup, every environment variable, and the six test fixtures.

It shipped four roles, two of which the approval removal killed the next day. Step 4 deletes
`campaigns:submit` and `campaigns:approve` and adds `campaigns:launch`; nothing here needs reopening.

The `EndpointDataSource` guard test lands here even though it proves nothing until step 4 gives it
something to protect.

**Blocked by:** nothing.

### 2. Templates — `02-templates.md`

The `template` table, the `InsertData` seeding migration, the derived-placeholder tests including the
inverted one for campaign templates, and the substitution engine.

Buildable entirely from unit tests. The seeded copy is provisional against what the client returns.

**Blocked by:** 1.

### 3. The events contract — `01-events-contract.md`

`visibility` and `language` on `Event`, in one PR across events-service, convocatorias-service, the
realm and both compose files, plus the public event republish.

The only step touching another service's code, and it moved up the queue on 2026-09-07. Step 5's
launch path reads the event for `{{evento}}`, `{{lugar}}` and `{{fecha}}`, refuses a cancelled one,
and picks the template by the event's `language`. That is a blocker, not a preference, and this file
used to soften it into one.

**Blocked by:** 1. Ships a frontend catch-up card (step 10).

### 4. Campaigns and launch — `03-campaigns.md`

The three tables, both state machines, the launch guards with `expectedRecipientCount` and the two
empty-campaign codes, recipient selection from CRM, and `campaign_exclusion`.

**It also removes the approval roles from the realm.** `campaigns:submit` and `campaigns:approve`
were written in step 1 against the old plan; this step replaces them with `campaigns:launch` in
`AuthorizationPolicies.cs`, `infra/keycloak/pgti360-realm.json`, the authorization tests, and the
ADR-0016 console step in `docs/deploy-with-dokploy.md`. Realm changes land twice, and a deletion
is no different.

Smaller than it used to be, since the approval table, the gate and its two implementations are all
gone. Split it if it still will not fit a session: the entities and the two state machines first,
then selection and exclusions.

This is the PR that gives the guard test from step 1 something to guard.

**Blocked by:** 1, 2, and item 4 of the CRM ask for selection.

### 5. Launch, the 202 and the handoff worker — `04-send-and-handoff.md` §1, §3

The launch endpoint with all five refusals, the unsubscribe flush, the mailability re-check, the
event read, the flip to `sending`, the `202` with its `Location`, and the worker: a
`BackgroundService` draining a `Channel<Guid>`.

**This step lands the platform's first background execution.** Read ADR-0018 before starting it. The
rule that no handler `CancellationToken` reaches the batch loop is the part worth a test of its own,
because getting it wrong looks fine locally and fails on a closed laptop.

`ICampaignSendGateway` is defined here with a test fake behind it. The real one is step 6.

**Blocked by:** 3, 4, and item 10 of the CRM ask.

### 6. The notification gateway, batching and resume — `04-send-and-handoff.md` §2, §4, §5, §6

`StubCampaignSendGateway` and its table, `Notifications__Transport` across both compose files, the
notification wire shape, chunking at 1,000, the per-recipient failure mapping, and the two resume
routes.

After this the service does its whole primary job end to end against the stub, with no dependency on
notification-service existing or on the client choosing a provider.

**Blocked by:** 5.

### 7. Recipient uploads — `05-recipient-uploads.md`

The two staging tables, file validation, the six buckets, in-file dedupe, the confirmation write and
its circuit-breaker behaviour, and the three size codes.

Independent of the portal, so it can run in parallel with 8. **Its column mapping is provisional**;
keep it in one place.

**Blocked by:** 4, and items 3 and 4 of the CRM ask.

### 8. The registration portal — `06-registration-portal.md`

The seven public endpoints, both token types, the two codes, QR generation, the registration write
with its CRM sync columns, the confirmation email, and unsubscribe with its write-through. Plus the
registration window (§5): one table, two authenticated endpoints, `registrationOpen` on both public
event payloads, and the `409` on resolve and submit. It lands here rather than in its own step because
the guards it adds are guards on endpoints this step is already writing.

**Blocked by:** 6 (the confirmation email needs a real send gateway), 3 (the public event read), and
items 3 and 7 of the CRM ask. Ships a frontend catch-up card (step 10).

### 9. Tracking and reconciliation — `07-tracking-and-reconciliation.md`

The rollup, the participant union, the five filters, the streaming CSV, and the four reconciliation
buckets, plus `registrationWindow` on the rollup, which is a read of the table step 8 creates. `strandedCampaigns` is the one steps 5 and 6 create the need for; its two actions live on
the campaign routes from step 4.

Last on the backend because it reads everything the earlier steps write. **Settle the CSV delimiter on
a real MITUR laptop before this ships.**

**Blocked by:** 6, 7, 8.

### 10. Frontend catch-up — Roberto's, own card, may lag

Per the root `CLAUDE.md` exception, `frontend/` may lag a cross-service contract change. Two cards
accumulate here:

- From step 3: a visibility control on the event form, stop sending `language: null`, drop
  `invitedCount` and `mapEmbedUrl` from the public pages.
- From step 8: the nine change requests in `06` §7, of which deleting the whitelist path and
  unlearning the template block builder are the real work.

**Blocked by:** 3, 8.

**No card owns the campaign screens.** The mock derives campaign status from the event and has no
launch button, so nothing there broke when approval went away. But `202`-and-poll is a contract a
launch button has to know about, and there is nowhere for it to land yet.

## What is not in this queue

- **ADR-0017** shipped on 2026-09-04, ahead of step 1, and was amended on 2026-09-07 when the client
  removed the approval step.
- **ADR-0018**, the launch handoff, is written and ships with step 5, the step it governs.
- **The two CRM spec amendments** (`08` §3) shipped in the same PR as ADR-0017.
- **The two outstanding client answers.** Neither blocks any step. When both land they get one
  grilling session together: the real row count re-derives `05` §7's ceiling, and the real template set
  may cut `02` §7's keys differently.
