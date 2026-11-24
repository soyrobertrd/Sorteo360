# Notifications — Build Plan Overview

Plain-language overview of the notifications effort. This page is the index; the numbered specs
beside it hold the detail, and `BUILD-ORDER.md` is the queue to work them in.

**Planning is complete.** Every decision below came out of the Notifications Wayfinding map
(2026-08-04 to 2026-09-13), whose eleven closed tickets are the reasoning behind each line here. The
boundary decision is `docs/adr/0020`; ADR-0011 is amended by it, and ADR-0013 and ADR-0017 are its
neighbours. Several client answers are outstanding and **none of them blocks the build** — see
"What the client still owes us".

Last updated: 2026-09-13.

## What we are building

A new `notification-service` that sends email and remembers every message it sent, plus
events-service as its first caller.

1. **A send pipe.** A caller POSTs a from-address, a subject, an HTML body with `{{placeholders}}`
   still in it, a data bag and a list of addresses. The service substitutes, sends over SMTP, and
   records.
2. **A send history.** The client's own requirement, and the thing that decided the service
   boundary: a browsable record of every email, with statuses, filters and a read API.
3. **Delivery feedback.** SendGrid's signed event webhook writes delivered / bounced / unsubscribed
   back onto the rows.
4. **Four cotización alerts** from events-service: submitted, approved, rejected, withdrawn.

The service's own vocabulary is `notification-service/GLOSSARY.md`. Read it before the specs.

## Decisions already locked

- **The service is standalone with its own database**, because a browsable history is *domain* and
  the root `CLAUDE.md` charters the shared chassis for technical scaffolding only. The chassis was
  the cheapest option and was the recommendation until the history requirement landed.
- **It holds zero copy and zero domain knowledge.** No template table, no default body, no OPT
  awareness. The consequence, accepted out loud: **it cannot send an email of its own.**
- **Two questions, two owners.** *Who is this person?* is **resolution** and belongs to the caller.
  *Can this address receive mail?* is **deliverability** and belongs to this service, because the
  answer arrives on its webhook and reaches nobody else.
- **Two recipient kinds, and the pipe must not tell them apart.** Internal (a Keycloak account,
  always mailable, no unsubscribe link) versus external (a CRM contact, gated by `canBeEmailed`,
  carrying `{{enlace_baja}}`). Both arrive as `{ email, name?, data? }`.
- **A failed notification never fails a business transaction.** The caller posts after its own
  commit and swallows the outcome — and the seam returns a type with **no error channel**, so the
  swallow is structural rather than a rule somebody re-breaks.
- **The `202` is the line.** After it a history row exists; before it nothing does.
- **Sending is out of band**, forced by the transport: a 1,000-recipient send is ~1,000 sequential
  SMTP transactions, so a background worker drains them.
- **The idempotency key names the send, not the thing the send is about**, never expires, and a
  repeat replays the original ids rather than answering `409`.
- **The history stores the body pre-substitution**, so a live registration token can never be inside
  it. The safety is structural, not a policy.
- **`notifications:send` is granted to service accounts and to no human composite, ever.**
- **The SendGrid event webhook is consumed in phase 1**, and it is the platform's only anonymous
  inbound endpoint from a third party.
- **Mailpit is the sink, permanently** — for development, for tests, and as stage's guardrail. It is
  not scaffolding.
- **The transport is SMTP relay**, not the Web API. That is what forced C# rendering (SendGrid's
  Dynamic Transactional Templates are unreachable over SMTP) and the out-of-band worker.

## The specs

| # | Spec | What it covers |
| --- | --- | --- |
| 00 | [`00-foundation.md`](00-foundation.md) | The new service: nine-step runbook, realm, database, config, test fixtures |
| 01 | [`01-accept-and-persist.md`](01-accept-and-persist.md) | The wire contract, validation, idempotency, the four tables, the `202` |
| 02 | [`02-rendering.md`](02-rendering.md) | Substitution, HTML escaping, the two MIME parts |
| 03 | [`03-smtp-worker-and-dev-loop.md`](03-smtp-worker-and-dev-loop.md) | The drain worker, the SMTP leg, retry, suppression, the guardrail, Mailpit |
| 04 | [`04-event-webhook.md`](04-event-webhook.md) | Signed inbound events, correlation, orphans, status derivation |
| 05 | [`05-history-read-api.md`](05-history-read-api.md) | The read endpoints, who may read them, retention |
| 06 | [`06-events-service-integration.md`](06-events-service-integration.md) | **events-service work**: four triggers, recipient resolution, templates, three env vars |

Each spec is written to be executable **without any AI tooling**: behaviour, data model, endpoints,
rules and a Definition of Done in plain markdown.

## Build order

```
00 Foundation ──► 01 Accept & persist ──┬──► 03 Send (worker) ──► 04 Webhook
                  (incl. 02 Rendering)  ├──► 05 History read API
                                        └──► 06 events-service triggers
```

**`BUILD-ORDER.md` is the queue** — six tickets, each with its own checklist. Start there rather than
reading the specs end to end. The specs are seven numbered files; `02` (rendering) is built inside
the same ticket as `01`, because an engine with no endpoint is not demoable on its own.

> **Every ticket is buildable today with no SendGrid credentials.** Mailpit is a real SMTP server,
> so the production path runs against it unchanged; the webhook is exercised with a keypair we
> generate. **Tickets 1 to 3 land a real email in a real inbox** — that is the demo.

## Things that are deliberately absent

Each of these was considered and rejected for a specific reason. The reason is in ADR-0020.

- **A resend button.** We store no `data`, so a placeholder-bearing batch cannot be rebuilt, and the
  button would send something other than what the screen shows.
- **A recipient allowlist** as the non-production safety net. The sink is the guard, and it inspects
  the **credential**, not the hostname.
- **A stub SMTP adapter.** It would invert the risk: the real connection, STARTTLS, AUTH and MIME
  assembly would first execute in production.
- **A notification type registry.** ADR-0017 made the caller supply its own template, so a registry
  would validate a payload against a template it has never seen. `type` survives as a history label.
- **EU data residency.** It moves only SendGrid's copy while our on-premises database stays the
  system of record, and it deletes the Activity Feed.
- **A retention purge job.** One policy, indefinite. For a government agency a records rule is
  likelier a minimum than a maximum, so an invented deletion schedule could itself be the compliance
  error.
- **OPT scoping on the history.** Unreachable, not declined: the service knows no `opt_code` and has
  no outbound client to ask with.
- **A message broker.** ADR-0005 stands.

## Honest limits, stated rather than engineered away

- **A crash between a caller's commit and its POST loses that notification silently and
  permanently.** The price of having no outbox.
- **Nothing alerts and nobody is paged.** A failure is discovered by looking — on the quote, or on
  the history filtered to `failed`.
- **A caller can inline personal data into `body` and we will store it.** A documented obligation
  enforced by nothing, because a body scanner false-positives on an `info@mitur.gob.do` footer and
  false-negatives on a person's name.
- **`notifications:send` reaching a human composite in the Keycloak console is undetectable by any
  test**, because ADR-0016 gives the console ownership of the realm. Which is why the rule is written
  in words.

## What the client still owes us

None of these blocks building. Chase them early anyway — they have lead time and they are someone
else's queue.

**With MITUR's IT team** (one email, three lines): open **outbound port 587** on the stage host —
25, 587, 465 and 2525 are all dropped today, measured; confirm the same for production; and confirm
SendGrid may **POST inbound** to the production host on 443. Stage's inbound already works, measured.

**With the client's SendGrid administrator:**

| # | Ask | Why it matters |
| --- | --- | --- |
| 1 | Domain authentication DNS records published | Blocks any production send and removing the *"via sendgrid.net"* suffix |
| 2 | Which plan tier | Essentials versus Pro decides dedicated IPs and subusers |
| 3 | The full per-office from-address list (ATL / NYC) | Campaign mail only — **does not block** the four cotización alerts |
| 4 | A written answer on message-body retention | For the DPA; the public docs contradict themselves |
| 5 | **How long must the send history be kept?** Ask as a **minimum** | We keep indefinitely; a minimum tells us whether that is right |
| 6 | How many event webhooks may one account have? | Decides whether stage and production can both be observed |
| 7 | An API key scoped to **Mail Send only**, one per environment, plus the webhook public key | Short lead time, needed at integration |

**With MITUR on process** (ADR-0011's standing question, and it stays open by design): is the
approver Dirección or a comité? The recipient rule is *whoever holds `quotes:approve`*, so the answer
is a realm edit and no code.

**What we do not need:** a SendGrid development account. The free tier is gone, and we build against
Mailpit.

## Related work, tracked separately

- **Delete the unenforced `crm:service-read` role** — decided here, executed in `CRM Build`.
- **The standalone history screen** — built in `frontend/` (the real SPA, Roberto's) as Reportes
  section 5, reading the API in `05` through the app host route (ADR-0023, PGTI-105).
- **Convocatorias as the second caller.** Its contract is already amended for it; nothing here waits
  on it.
- **Open questions, deliberately not specified:** reconciling a hard bounce with CRM consent (they
  must not be collapsed — a typo would look like a withdrawn consent); per-type notification
  preferences and who may un-suppress an address; an operational email from the service about
  itself; nudging a silent approver, which would need the monorepo's first scheduled job; and SMS,
  where only the seam's shape is settled.
