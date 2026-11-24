# ADR-0023 — The SPA reads the notification history through the app host

- Status: Accepted
- Date: 2026-09-23
- Amends: `docs/specs/notifications/04-event-webhook.md` §3 (the history read API is no longer
  internal-only)
- Builds on ADR-0020 (notification-service owns the send history and the read API that serves the
  client's history screen)

## Context

ADR-0020 gave notification-service a read API *"that serves the client's history screen"*, and the
Reportes screen has a section for it, *Auditoría de notificaciones y correos*, running on six
hard-coded rows. Nothing connects the two. The frontend's nginx and the Vite dev proxy send every
`/v1/` path they do not list to events-service, so `GET /v1/notifications` from the browser answers
`404`.

Spec 04 §3, written while deciding how SendGrid's webhook reaches the service, said *"the send API
and the history read API stay internal-only"*. What it rejected there was a **whole public host**
for the service: every email MITUR has sent, on the internet, behind nothing but a Keycloak token.
It did not say how a screen would read the history, and a screen has to read it from somewhere.

Two routes were on the table: the browser calls notification-service through the app host, the
same way it already reaches crm-service and convocatorias-service; or events-service grows a
backend-for-frontend endpoint that reads the history on the browser's behalf.

## Decision

**The browser reaches the send history through the app host, GET only, on two exact paths.** The
frontend's nginx routes `GET /v1/notifications` and `GET /v1/notifications/batches/{id}` to
notification-service, and the Vite dev proxy routes the same two paths to its host port.

- **GET only.** nginx answers `403` to any other method on those paths (`limit_except GET`, which
  also lets `HEAD` through, as nginx always does). The send endpoint shares the list's path, and a
  person has no business reaching it; the `notifications:send` policy already refuses a human
  token, so this is a second lock, not the only one. The Vite dev proxy has no method filter, so in
  development that policy is the only lock.
- **Exact paths.** `/v1/notifications` is an exact match, and `/v1/notifications/batches/{id}` a
  regex that allows one segment after `batches/` and nothing below it.
  `/v1/notifications/orphan-events` is **not** routed: no screen reads it, so it falls through to
  events-service and answers `404`. Opening it later is a one-line change when a screen needs it.
- **The role is the gate.** Both routes require `notifications:read`, which ADR-0020 put in
  `central_admin` alone. The screen gates its section on the same role, and the service refuses
  anyone without it either way.

The realm already carries what the token needs: the `audiencia-notifications-api` mapper on
`pgti360-app` puts `notifications-api` in the SPA's audience. No realm change comes with this
decision.

## Why not a BFF in events-service

- **Any screen puts the data behind a token anyway.** Whether the browser calls notification-service
  or events-service, the rows reach a browser holding a Keycloak token with `notifications:read`. A
  proxy in between adds a hop, not a lock.
- **events-service's own token sees only `events` rows.** Spec 05 §2 scopes a service account's read
  to rows whose `about.source_service` is itself. A BFF calling with its client-credentials token
  would show an administrator the quote emails and nothing from convocatorias. Forwarding the
  person's token instead would make the BFF a pass-through with extra code.
- **It would put notification history into events-service**, a service with no business owning it
  and already the largest on the platform.

## What still holds from spec 04 §3

The service gets **no public host and no Traefik router of its own**. The browser reaches exactly
the two read paths above through `app.${DOMAIN}`, and nothing else of notification-service is
reachable from the internet: not the send API, not `orphan-events`, not `/scalar`. The webhook keeps
its own host, `hooks.${DOMAIN}`, for the reasons spec 04 §3 gives.

## Consequences

- nginx and the Vite proxy each gain two entries. They are twin lists that nothing compares, the
  same standing risk the CRM and convocatorias routes already carry.
- A person with `notifications:read` can call the history API directly with their token, outside
  the screen. That is the same exposure the screen itself creates, and it is what the role is for.
- Widening who sees the history stays a realm edit (granting `notifications:read` to another
  composite) with no code change.
- Spec 04 §3 is edited in the same change to point here.
