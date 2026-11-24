# notification-service: the SendGrid event webhook

> Moved from `notification-service/CLAUDE.md` (PGTI-215), text unchanged except headings. A section named
> with "see ... below" or "§..." may now live in a sibling file in this folder: `grep -n '^## ' notification-service/docs/*.md` finds it.

## The webhook is the platform's only anonymous inbound endpoint from a third party

`POST /hooks/sendgrid/events`, no Keycloak policy, an ECDSA signature verified **before the body is
parsed**. It is flagged for security review, and the review surface is exactly: the raw-body read,
signature-before-parse, the 10-minute replay window, the 2 MB body cap, and the absence of any
Keycloak policy. Spec `04`. All five live in `SendGridEventWebhookEndpoint.Receive`, in that order,
and the order **is** the spec — parsing before verifying breaks verification *silently*, because
model binding re-serializes and what was signed were the bytes.

Everything else on this service is closed. `/_alive`, `/_ready`, `/_health` and the API docs are
the only other anonymous routes; `AnonymousRoutesTests` walks the registered route table and fails
if a sixth appears, and `FallbackPolicyProbeEndpoint` exists to prove the fallback policy still
shuts a route that forgets `.RequireAuthorization`.

> **Traefik publishes it with `Path`, never `PathPrefix`.** Traefik matches the raw request path
> and Kestrel resolves `..` segments before routing, so a prefix rule lets
> `/hooks/sendgrid/events/../../openapi/v1.json` through to the anonymous doc surface. An exact path
> costs nothing here — the endpoint answers exactly one — and closes that disagreement.

> **This one route is exempt from the platform's machine-`code` ProblemDetails rule** (root
> `CLAUDE.md`), and the exemption is deliberate rather than an oversight. Its four failures answer a
> bare `403`, `413` or `400` with no body, because the caller is a third party on the open internet:
> telling it *which* of the four checks it failed tells it how to get closer. The reason goes to the
> log instead, and this is **the only endpoint in the platform that logs on its own** — every other
> one hands the reason back to a person or a sibling service that can read it, whereas SendGrid will
> never tell anyone our public key is wrong.

## The webhook's rules

- **The webhook's rules are pure and live in `Core/Webhooks/`**: `EventWebhookSignature` (verify
  and the replay window), `DeliveryLadder` (what each event type asserts, and the precedence that
  makes a late `processed` unable to demote a `delivered`), `EventCorrelation` (the three-rung
  ladder, and what counts as an *open* send), `EventDedupeKey` (the composite hash for events with
  no `sg_event_id`), `SendGridEventBatch` (the parse, which runs **after** the verify). The only
  thing in Infrastructure is `NotificationEventIngest`: one transaction over the whole batch,
  `ON CONFLICT DO NOTHING` untargeted so both unique indexes dedupe, and `FOR UPDATE` on each row
  it recomputes.
- **Delivery and reaction are two facts and never share a column.** `delivery_status` is how far
  the mail got; `recipient_action` is what the person did. A `spamreport` proves the mail *was*
  delivered and sets `complained` — an earlier draft stacked them in one precedence ladder and that
  was a modelling error, recorded in `DeliveryLadder` so it is not re-made.
- **Status is derived, never overwritten.** The event log is append-only and the webhook is
  at-least-once and unordered, so `DeliveryLadder.Advance` takes a maximum rather than assigning.
  Taking the max one event at a time equals recomputing over the whole log, because max is
  associative and commutative — that is what lets the ingest write status per insert.
- **The dev keypair is generated, never committed**, and `.secrets/` is gitignored.
  `scripts/sendgrid-fake-events.sh` creates it on first run, prints the public key to paste into
  `.env`, and POSTs a correctly signed fake batch at `localhost` — it refuses any other host,
  because manufacturing delivery confirmations in stage is the one lie this design must not tell.
