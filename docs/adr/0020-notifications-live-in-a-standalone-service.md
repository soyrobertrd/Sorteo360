# ADR-0020 — Notifications live in a standalone notification-service that owns the send history

- Status: Accepted
- Date: 2026-09-13
- Supersedes in part ADR-0011 (the email it puts out of scope now exists; see its Consecuencias)
- Builds on ADR-0013 (service-to-service calls), ADR-0017 (a caller supplies its own template)

## Context

The request arrived as *"events-service needs a proper notification system"*. Charting established
it is a **platform** concern with a **new service** at the end of it: nothing in the monorepo sends
email today, and the only seam that exists, `IApprovalGate` with `StubApprovalGate` behind it,
writes a log line and returns a fake reference.

Five facts shaped the decision, and the order matters — the first is what moved the boundary.

**The client wants to browse a history of every email sent.** This surfaced mid-charting. It is a
product feature with a screen, statuses and filters, not plumbing. Before it landed, the
recommendation was the shared chassis.

**There is no SendGrid free tier.** It was retired 2025-05-27, leaving a 60-day trial, and sandbox
mode is not an offline mode — it needs a real API key and explicitly does not validate template
content. So the platform cannot be built against the provider, and a local development path is
required rather than merely preferred.

**SendGrid cannot be the history.** Activity retention is 3 days on Essentials and 7 on
Pro/Premier, and the Email Activity API is add-on gated at 6 requests per minute and unavailable to
EU subusers. Our database has to be the system of record, which the client's own requirement
already implied.

**The client chose SMTP relay, not the Web API.** `smtp.sendgrid.net`, with the API key as the SMTP
password. That removed options rather than losing arguments to them: SendGrid's Dynamic
Transactional Templates are unreachable over SMTP (*"The X-SMTPAPI does not support Dynamic
Transactional Templates"*), so rendering in C# is forced, and a 1,000-recipient send is ~1,000
sequential SMTP transactions rather than one call, so sending cannot happen inside the caller's
request.

**ADR-0017 got there first.** Convocatorias decided a caller supplies its own template and that
notification-service is a sending gateway. That is now the general rule, and it deleted the type
registry this map had originally specified.

## Decision

**Notifications live in a standalone `notification-service` with its own database.** It owns
sending, retry, the SMTP credential, the send history, the suppression list and the read API that
serves the client's history screen. Callers POST and move on.

**The service holds zero copy and zero domain knowledge.** It has no template table, no default
body, no notion of an OPT, an evento or a cotización. It receives a from-address, a subject, a body
with `{{placeholders}}` intact, a data bag and a list of addresses; it substitutes, sends and
records. The consequence is accepted out loud: **it cannot send an email of its own**, not even to
tell an operator that a batch failed.

**Two questions, two owners.** *Who is this person and what is their address?* is **resolution**,
and it belongs to the caller, which is the only party that knows what a role or a campaign means.
*Can this address receive mail at all?* is **deliverability**, and it belongs to
notification-service, because the answer arrives on its webhook and reaches nobody else. The pipe
never decides who should be told; it refuses only addresses the provider has reported dead or
hostile, and records the refusal as a history row rather than dropping it.

**Two recipient kinds, and the pipe must not be able to tell them apart.** An **internal**
recipient is a Keycloak account, address of record Keycloak, always mailable, no unsubscribe link.
An **external** recipient is a CRM contact, address of record `email_normalized`, gated by
`canBeEmailed`, carrying `{{enlace_baja}}`. Both arrive as `{ email, data? }`. Every difference
lives in the caller.

**A failed notification never fails a business transaction.** The caller posts **after** its own
commit, swallows the outcome and never retries. The seam returns a type with **no error channel**,
so the swallow is structural rather than a rule somebody re-breaks — which is exactly how this
charter came to be written once and never implemented.

**The 202 is the line: after it a history row exists, before it nothing does.** A malformed request
is our bug and answers `400` having stored nothing. A world failure — SMTP down, a bounce, a
suppression — answers `202` and is stored with a status. The client asked to browse the emails we
sent, not the bugs we wrote.

**Sending is out of band.** The request validates, persists and returns `202`; a background worker
drains the persisted per-recipient rows over one reused SMTP connection. This is forced by the
transport, not chosen.

**The idempotency key names the send, not the thing the send is about.** It is caller-minted and
derived from a natural key, never random, and it never expires. A repeat answers `202` replaying
the original ids — never a `409`, which is a failure status the caller was told to swallow, and so
a false alarm about a request that worked. This is what makes the chassis's blind retry safe: the
shipped `ServiceResiliencePipeline` is **verb-agnostic** and re-POSTs today, so idempotency is not
a refinement on a safe transport, it is what makes the transport safe for writes.

**The history stores the body pre-substitution.** The caller's `body` still carries its
`{{placeholders}}`, and a live credential — convocatorias' registration token — lives only in
`data`, which is stored nowhere at all. The safety is therefore **structural**: there is no
scrubbing rule to get wrong and nothing a later edit can weaken. The accepted cost is that the
history shows what a campaign *said*, not the precise bytes one named person received.

**`notifications:send` is a realm function role granted to caller service accounts and to no human
composite, ever.** This is the one place notifications departs from the crm precedent, and it
departs on that precedent's own logic: a directory read a coordinator can already do in the UI
gains nothing from a role, while an endpoint that cannot tell a person's token from a machine's
would let any logged-in coordinator send mail as MITUR, from MITUR's authenticated domain, with an
effect that leaves the platform and cannot be retracted.

The rule that generalises, which no ADR currently carries and which the next service will copy:

> **A service account holds a function role only where that role gates an endpoint. Where a person
> and a machine may legitimately read the same thing, the fallback policy is the gate and there is
> no role.**

**The SendGrid event webhook is consumed in phase 1.** SMTP is a one-way door — `250 Ok: queued`
and the conversation ends — so without the webhook the history lies: a suppression drop is silent,
still consumes a credit, and the row reads `sent` forever. The road is already built, because the
drain worker exists regardless and the webhook is a second writer to it.

**This is the platform's only anonymous inbound endpoint from a third party, and it is flagged for
security review.** The review surface is exactly: the raw-body read, signature verification before
parsing, the 10-minute replay window, the 2 MB body cap, and the absence of any Keycloak policy.

**Mailpit is the development and test sink, permanently, and stage's guardrail permanently.** It is
a real SMTP server, so the production code path runs against it unchanged and the "switch" to real
sending is `Smtp__Host`. **It is not scaffolding and nobody should delete it later thinking it
was.**

### Rejected

- **The shared chassis.** The cheapest option and the recommendation until the history requirement
  landed. The root `CLAUDE.md` charters the chassis for technical scaffolding and never for domain,
  and a browsable send history with statuses and a client-facing screen is domain. The history is
  what pays for the service.
- **A shared notifications database** written by several services. A distributed monolith: the
  one-database-per-service rule exists to stop schema coupling, and here there are no joins to buy,
  because a notification record is self-contained.
- **notification-service resolving recipients** from `{ optId, role }`. It would import crm-service
  and Keycloak and both of their outages, plus an outbound confidential client, to save the caller
  a lookup it is better placed to do. A service that cannot write a sentence of its own has no
  business deciding who deserves to read one.
- **A `NotificationTypeDefinition` registry.** Its premise was *the service that renders is the
  service that declares what it needs*; ADR-0017 broke that premise, and the registry would be
  validating a payload against a template it has never seen. `type` survives as a **history label**
  — a plain string, stored verbatim with no foreign key, so rows about a retired type still read
  correctly.
- **An `ISmtpTransport` with a logging adapter behind it.** It inverts the risk: connection,
  STARTTLS, AUTH and MIME assembly would execute for the first time in production, on the one run
  where nobody can watch them fail. The only genuinely untested thing on the day SendGrid arrives
  should be SendGrid.
- **A recipient allowlist as the non-production safety net.** A domain check in a service
  deliberately stripped of domain knowledge, and it fails in the wrong direction — *empty means
  block everything* kills production the day someone forgets the variable. **The guard is the sink,
  and it inspects the credential rather than the hostname**: a hostname check is a guess (DNS
  moves, aliases get renamed, `smtp.sendgrid.net` has twelve A records), while the API key *is* the
  SMTP password, so without it a send is impossible by any route.
- **`fullScopeAllowed: false` on `pgti360-app`.** Recorded by name so the move is on the shelf. It
  is the only Keycloak-enforced lever against a role reaching a person, but it is a realm-wide
  change decided for one endpoint, and it trades a **loud** failure (a person visibly holds a role
  they should not) for a **silent** one (a role legitimately granted, forgotten in the scope list,
  absent from the token, and a 403 nobody can explain). The property is absent from all six clients
  in the realm JSON, so every one of them currently defaults to on.
- **EU data residency.** It moves only SendGrid's copy while our on-premises database stays the
  system of record; we deliberately keep almost nothing there; it costs Pro plus a dedicated EU IP
  plus a brand-new subuser that cannot be migrated into; and it deletes the Activity Feed, the only
  UI window when something goes wrong. *"Cannot be retrofitted"* barely bites precisely **because
  we hold the history ourselves** — a future EU campaign is a fresh subuser, not a migration.
- **A resend button.** We store no `data`, so a placeholder-bearing batch cannot be rebuilt, and the
  button would send something other than what the screen shows. Re-triggering already exists
  upstream in both callers. The history is therefore read-only and has no write authorization to
  design.
- **A message broker.** ADR-0005 stands. The place one would earn its keep is retry and delivery
  feedback inside this service, and both are answered by the drain worker and the webhook.

## Consequences

- **A new service, and the nine-step runbook applies in full** (`docs/adding-a-new-service.md`):
  chassis by `ProjectReference` from Core/Infrastructure/Web, its `.csproj` in the `.slnx`, a
  Dockerfile built from the **repo-root** context, a compose block with `profiles: [all,
  notifications]`, an empty `CREATE DATABASE` in `infra/postgres/init.sh`, a host port in
  `.env.example` and a README port-map row, its own `CLAUDE.md`, and its realm client. Specs that
  assume this instead of carrying it produce a service that does not build.
- **Callers get a one-way seam.** events-service's `IApprovalGate` and `StubApprovalGate` are
  **deleted**, not occupied — the name is ADR-0005's label for a request that no longer exists, the
  `Result<string, BaseError>` invites the branch this ADR forbids, it takes a `Quote` where the key
  and the copy need the attempt, and it covers one moment of four. `IQuoteNotifier` replaces it. See
  ADR-0011's amended Consecuencias.
- **A crash between a caller's commit and its POST loses that notification silently and
  permanently.** Nothing remembers it was owed and the business record looks normal. That is the
  price of having no outbox, and it is named here rather than buried.
- **Nothing alerts and nobody is paged.** A failure is discovered by looking — on the quote for the
  coordinator who submitted it, or on the history screen filtered to `failed` for an administrator.
  Proportionate at a handful of internal emails a day; a change in volume or in stakes is a new
  decision, not a gap in this one.
- **Mail we never attempted is not this service's business.** When a caller resolves nobody — Chile
  seeds with no director, and `gestor` carries no `opt_code` — it never calls, so no history row
  exists anywhere. The fact is recorded on the caller's own row instead.
- **The channel-agnostic seam makes SMS a later adapter.** Only the shape is settled here: no
  provider, no phone-number sourcing, no SMS work in this effort.
- **Realm changes, landing twice per ADR-0016** — in `infra/keycloak/pgti360-realm.json` for realms
  created later, and as console steps in `docs/deploy-with-dokploy.md` for the ones running: a
  `notifications-api` audience client; the function roles `notifications:send` and
  `notifications:read`; `notifications:read` in `central_admin` alone to start;
  `notifications:send` in **no** composite; an `audiencia-notifications-api` mapper on each calling
  service's confidential client; `notifications:send` on each calling service account; and
  `view-realm` + `view-users` on `service-account-events-service` — **not `realm-admin`**.
- **No outbound confidential client for notification-service.** It calls nobody, and a secret
  nobody uses is a credential to store, ship and rotate for nothing.
- **`crm:service-read` was to be deleted rather than enforced**, under the rule above: it was granted
  to two service accounts, required by no policy anywhere, and could not be enforced on the OPT reads
  without darkening the frontend's dropdowns, which ADR-0013 deliberately puts on the fallback policy.
  **Superseded 2026-09-14 by `docs/adr/0015`'s company work, and the rule above is what supersedes
  it.** `GET /v1/companies/directory` — the bulk company snapshot events-service caches — is a
  machine-only route that no screen calls, and it requires the role: a caller without it gets 403. So
  the role does gate an endpoint, which is exactly what the rule asks of it, and it is kept. The
  reasoning that pointed at deletion is unchanged and still applies to the OPT reads, which keep the
  fallback policy. Deletion is not to be done, and the `CRM Build` card that tracks it is obsolete.
- **A caller may inline personal data into `body`, and nothing stops it.** Scanning at accept time
  was rejected: it false-positives on a legitimate `info@mitur.gob.do` footer and false-negatives on
  a plain person's name, and **a check that can be wrong in both directions is worse than a rule a
  person can read.** It is a documented caller obligation enforced by nothing, said out loud rather
  than implying a guarantee we do not have.
- **Retention is one policy — indefinite, with no purge job.** Volume is trivial, the privacy
  surface is one column, and for a government agency a records rule is far likelier to be a
  *minimum* keep-period than a maximum, so an invented deletion schedule could itself be the
  compliance error. Erasure, when crm-service's own lands, **redacts the address and never deletes
  the row**, mirroring crm's decided rule. It does not call SendGrid's `erasejob`: that is Web API
  v3 and our Mail-Send-scoped key cannot reach it.
- **Delivery tracking is per-environment, and the history says so.** `tracking_expected` is stamped
  on each row at insert from configuration, never read as a global flag, so a row written while no
  webhook existed stays honest after someone enables one. **Normative: when it is false,
  `delivery_status` never passes `sent`** — otherwise a permanent `sent` reads as a stuck queue.
  Stage is deliberately such an environment.
- **Stage runs Mailpit permanently and never sends real mail**, because a stage run of the
  cotización flow would mail Dirección a genuine-looking *"tu cotización fue rechazada."* The
  blocked outbound SMTP ports are not the reason and lifting them changes nothing. Mailpit there
  needs `MP_UI_AUTH` (stage is public through Traefik) and a **named volume in
  `docker-compose.dokploy.yml`**, or a redeploy wipes the inbox while the history rows still say
  `sent`.
- **Open with MITUR, blocking nothing:** whether the approver is Dirección or a comité (ADR-0011's
  question, kept honestly open by defining the recipient as *whoever holds `quotes:approve`*), the
  per-office from-address list, the plan tier, a written body-retention answer, a history-retention
  minimum, how many event webhooks one account may have, and whether SendGrid may POST inbound to
  the production host. The outbound SMTP port on the stage host is with MITUR's IT team.
