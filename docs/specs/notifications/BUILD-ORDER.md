# Notifications — Build Order

Six tickets in dependency order. Read the specs for the *why* and the detail; this page is the
order to build in and what "done" means for each step. Each ticket is sized to be finished, tested
and reviewed on its own.

## The headline

> **Every ticket below is buildable today, with no SendGrid credentials and no client answer.**

That is not a lucky accident, it is the shape of the design. **Mailpit is a real SMTP server**, so
the production sending path runs against it unchanged and the only difference between local and
production is `Smtp__Host`. The webhook's ECDSA verification is exercised by a keypair we generate
ourselves. The one thing that genuinely needs the client is the real smoke send, which happens once,
from stage, after MITUR's IT team opens an outbound port — and it changes no code.

**Tickets 1 to 3 land a real email in a real inbox.** That is the demo.

## Before you start

**Read these, in this order:**

1. `docs/adr/0020-notifications-live-in-a-standalone-service.md` — the boundary and the reasoning.
   Several rules below exist because a plausible-looking alternative was considered and rejected for
   a specific reason, and the reason is in the ADR or in the spec prose above the tables.
2. `docs/adding-a-new-service.md` — the nine-step runbook. `00-foundation.md` answers each step for
   this service; it does not replace the runbook.
3. `docs/dotnet-service-conventions.md` — the conventions every .NET service here follows, including
   the patterns deliberately **not** used: no MediatR or CQRS, no AutoMapper, no controllers, no
   global exception handler, no `IUnitOfWork` layer.
4. `events-service/CLAUDE.md` — the origin of most conventions, and the service ticket 6 modifies.

**Three things to know before ticket 1**, because they shape everything:

1. **This service holds zero copy and zero domain knowledge.** No template table, no OPT, no
   evento. If a ticket seems to need one, the design has drifted.
2. **The `202` is the line.** After it a history row exists; before it nothing does. A malformed
   request is our bug and answers `400` having stored nothing.
3. **The caller resolves, the service delivers.** *Who is this person?* is the caller's question.
   *Can this address receive mail?* is ours.

**When something here is ambiguous, ask rather than guess.**

| # | Ticket | Blocked by | Spec |
| --- | --- | --- | --- |
| 1 | The service exists and starts | — start here | `00` |
| 2 | Accept a notification and persist it | 1 | `01` + `02` |
| 3 | Send it over SMTP with the drain worker | 2 | `03` |
| 4 | The send history read API | 2 | `05` |
| 5 | The SendGrid event webhook | 3 | `04` |
| 6 | events-service sends its four quote alerts | 2 | `06` |

**Three tickets open at once the moment 2 lands** — 3, 4 and 6 — and nothing forces a single straight
line after that. Only the webhook waits on the worker, because you cannot demonstrate *delivered*
without having sent.

> **The substitution engine is inside ticket 2, not a ticket of its own.** It had one in this file's
> first draft and it was merged in on 2026-09-13: an engine with no endpoint is not demoable, which
> breaks the rule every other ticket here follows, and it is exactly what produces ticket 2's most
> important `400`. The specs stay seven numbered files; only the ticket count changed.

---

## 1. The service exists and starts

**Blocked by:** nothing — start here. **Spec:** `00-foundation.md`

A new `notification-service` container that boots, applies an empty migration to its own database,
answers `/_alive` anonymously and `401`s everything else. Mailpit runs beside it. Nothing sends
anything.

This is the tracer bullet for the whole service and it cannot be cut smaller: the nine-step runbook
is all-or-nothing, because a service missing any one of its steps does not build or does not deploy.

- [ ] `docker compose build notification-service` succeeds from the repo root, and there is no
      `Directory.Build.props` / `Directory.Packages.props` under the service folder.
- [ ] `docker compose build postgres && docker compose down -v && docker compose up -d` leaves an
      empty `notifications` database and the first migration applies on startup.
- [ ] `GET /_alive` is 200 with no token; a route with no `.RequireAuthorization` still returns 401.
      **Prove the fallback by adding such a route once.**
- [ ] The eight realm changes are in `infra/keycloak/pgti360-realm.json` **and** as console steps in
      `docs/deploy-with-dokploy.md`, and a test asserts `notifications:send` is in no composite.
- [ ] Every variable in `00` §9 is in all three compose files; `scripts/compose-parity.sh` passes.
- [ ] Mailpit runs with **no Compose profile**, and `MAILPIT_UI_PORT` + `NOTIFICATIONS_PORT` are in
      `.env.example` and the README port map.
- [ ] `notification-service/CLAUDE.md` and `GLOSSARY.md` exist; `GLOSSARY-MAP.md` links the latter.
- [ ] The four test fixtures exist and one smoke test uses each.

## 2. Accept a notification and persist it

**Blocked by:** 1 · **Specs:** `01-accept-and-persist.md` **and** `02-rendering.md`

`POST /v1/notifications` end to end up to the `202`: the four tables, validation, idempotency, **and
the substitution engine**. The largest ticket, and it cannot be split further: the idempotency race
is settled by the unique index, so the schema and the endpoint have to land together, and the engine
is what produces the unresolved-placeholder `400`.

Build the engine first inside this ticket — it is pure, needs no container and no network, and its
unit tests are the cheapest feedback loop in the queue.

- [ ] A valid POST returns `202` + `Location`, one result per recipient, and creates the batch, its
      `about` rows and one recipient row per person.
- [ ] A repeat with the same key and body returns `202` with **the same ids** and creates nothing;
      the same key with a different body returns `409`.
- [ ] **Two identical POSTs fired concurrently produce one batch and two identical 202s, not a 500.**
- [ ] Each validation failure returns its `code` **and creates nothing** — assert the second half.
- [ ] 1,001 recipients is a 400; unknown fields and unknown `data` keys are ignored.
- [ ] The stored `body` still contains `{{placeholders}}`, and **no column anywhere holds a `data`
      value** — assert by grepping every column for a recognisable token.
- [ ] `tracking_expected` is stamped at insert and does not change when the setting is flipped.
- [ ] `notifications:send` gates the route; no human composite holds it.
- [ ] Flat literal `{{key}}` replacement from `sharedData` and a recipient's `data`, with **no**
      conditionals, loops, helpers or `{{{raw}}}`.
- [ ] Every substituted value is **HTML-escaped** — assert with `<script>` and a bare `&`.
- [ ] The `{{key}}` scanner names **which** key is unresolved, including one missing for only one
      recipient of several, and one that appears in the **subject** rather than the body.
- [ ] `multipart/alternative` with both parts present and distinct; the text part is **derived** from
      the rendered HTML (MimeKit's converter), never authored.
- [ ] The engine has no branch on `type`, no template lookup and no hard-coded key list — assert by
      rendering a placeholder the service has never seen.

## 3. Send it over SMTP with the drain worker

**Blocked by:** 2 · **Spec:** `03-smtp-worker-and-dev-loop.md`

The monorepo's first `BackgroundService`. After this ticket a real email arrives in Mailpit and
**the feature is demonstrable**.

- [ ] A message sent with `Smtp__Host: mailpit` appears in the Mailpit UI, with no code path
      differing from production except `Smtp__RequireTls`.
- [ ] The startup guard refuses to boot with `AllowExternalRelay=false` and a non-empty
      `Smtp__Password`, proven by a unit test.
- [ ] `4xx` retries, `5xx` never does, `535` is terminal **and** logged at error, retries stop after
      one hour.
- [ ] Two concurrent drainers send each row exactly once (`FOR UPDATE SKIP LOCKED`), and per-recipient
      status survives a part-way batch failure.
- [ ] A suppressed address produces a `dropped` history row — **not** a missing row.
- [ ] **Exactly one** Mailpit end-to-end test asserts the bytes: one message per recipient, `Cc` and
      `Bcc` empty, both MIME parts, `X-SMTPAPI` `unique_args` verbatim, `List-Unsubscribe` present
      for an external recipient and absent for an internal one.
- [ ] Stage's Mailpit has `MP_UI_AUTH` (proven by a 401) and a **named volume** in the Dokploy file.

## 4. The send history read API

**Blocked by:** 2 · **Spec:** `05-history-read-api.md`

The client's actual feature. Read-only, so there is no write authorization to design.

`/orphan-events` ships here and legitimately returns an empty list until ticket 5 lands.

- [ ] `PagedResponse<T>` verbatim from crm-service: defaults to 25, rejects above 100 with a 400.
- [ ] Every filter works; `entityType` + `entityId` returns rows from **two different batches** about
      one event.
- [ ] `email` is an exact match, not a `LIKE`.
- [ ] `batches/{batchId}` returns the **pre-substitution** body and is what a `202`'s `Location`
      resolves to.
- [ ] `central_admin` sees every `source_service`; a service account sees **only its own**, including
      by batch id.
- [ ] Every recipient carries `deliveryTrackingAvailable`, and a row on a `tracking_expected = false`
      batch never moves past `sent` — **assert by feeding it a `delivered` event**.
- [ ] There is no POST, PUT, PATCH or DELETE on any route in this spec.

## 5. The SendGrid event webhook

**Blocked by:** 3 · **Spec:** `04-event-webhook.md`

**Security review required before merge.** The review surface is the raw-body read,
signature-before-parse, the 10-minute window, the 2 MB cap, and the absence of any Keycloak policy.

- [ ] Verification runs against a **byte-for-byte captured fixture** — prove the test catches a
      re-serialization by adding a whitespace change and watching it fail.
- [ ] Bad signature → 403; an 11-minute-old timestamp → 403; a 3 MB body → 413 before being read.
- [ ] Re-POSTing an identical batch changes nothing; a batch with no `sg_event_id` dedupes on the
      composite hash.
- [ ] A `delivered` followed by a late `processed` stays `delivered`.
- [ ] A `spamreport` sets `recipient_action = complained` and **leaves `delivery_status = delivered`**.
- [ ] An unknown GUID becomes an orphan; an uncorrelated event with exactly one open send to that
      address links by email, with two open sends it becomes an orphan.
- [ ] An unrecognised event type is **stored** and ignored when deriving status.
- [ ] An `unsubscribe` suppresses the address and the next send to it is a `dropped` row.
- [ ] The dev keypair is **generated, never committed**, and the gitignore covers it.
- [ ] Traefik routes `hooks.${DOMAIN}` on the webhook path only; a test asserts this is the service's
      only non-health anonymous route.

## 6. events-service sends its four quote alerts

**Blocked by:** 2 (the contract). Best demoed after 3. · **Spec:** `06-events-service-integration.md`

This is **events-service work**, in the events-service repo folder and test suites. **ADR-0011's
amendment lands in this PR.**

- [ ] Submit, approve, reject and withdraw each post one notification; `DiscardAsync` posts nothing.
- [ ] `IApprovalGate` and `StubApprovalGate` are gone; `IQuoteNotifier` has **no error channel**, and
      `SubmitAsync` no longer returns a notification failure.
- [ ] **A notification failure of every kind leaves the business operation successful** — force a
      timeout, a 500 and a transport exception.
- [ ] Grep the repo for `opt_director` and `central_admin` in events-service code: neither appears.
- [ ] A user holding `quotes:approve` through a composite is found; the `opt_code` intersection is
      pinned by a test, because Keycloak silently ignores `?q=` on that endpoint.
- [ ] An OPT with no resolvable approver writes `no_recipient` on the attempt row, posts nothing, and
      the submit still succeeds. Same with Keycloak unreachable and a cold cache.
- [x] The four flat columns replace `approval_ref_id` and both statuses ship in
      `QuoteDetailResponse.Attempts`.
- [ ] A submit alert and a reject alert on the **same attempt** produce two distinct batches.
- [ ] The `template` table is seeded by `InsertData` (never `HasData`), four rows keyed
      `(template_key, "es")`, with golden-file tests per body.
- [ ] **All three environment variables are in `docker-compose.yml` and `docker-compose.dokploy.yml`,
      with `App__BaseUrl` reading `http://localhost:${FRONTEND_PORT:-5175}` in the first and
      `https://app.${DOMAIN}` in the second.** Parity script passes.
