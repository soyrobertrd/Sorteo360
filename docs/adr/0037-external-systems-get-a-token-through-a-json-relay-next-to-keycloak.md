# ADR-0037 — External Systems get a token through a JSON relay next to Keycloak

- Status: Accepted
- Date: 2026-10-30
- Follows from: ADR-0004 (identity is Keycloak), ADR-0012 (the shared chassis), ADR-0025 (the
  fallback policy requires a staff or service role)

## Context

SoftExpert, MITUR's payments system, has to call events-service (PGTI-326). It is the platform's
first External System: software outside MITUR that calls the API under its own identity. Its app
can send JSON bodies, headers and query parameters, and can carry a value from one response into
the next request's header. It cannot send `application/x-www-form-urlencoded` and has no OAuth2
option, so it cannot speak to Keycloak's token endpoint, which accepts nothing else.

Keycloak is already public at `https://identidad.${DOMAIN}`. What SoftExpert lacks is the body
format, not a way in.

The alternatives were a long-lived token or an API key scheme. Both put a second credential model
beside Keycloak: a key store, its own rotation and its own validation in every service, and a
caller the authorization policies would have to recognise some other way than by its roles.

More External Systems are expected. A token is valid on every service whatever URL handed it out,
so the platform needs one relay, not one per service.

## Decision

**A token relay accepts `{ "clientId", "clientSecret" }` as JSON, sends Keycloak a
`client_credentials` grant, and returns Keycloak's success answer unchanged.** The caller then uses
`Authorization: Bearer <token>` like any service account.

- **OAuth2 straight to Keycloak is the preferred way in.** An External System that can send a
  `client_credentials` grant to Keycloak does so, and is never put on the relay's allow list. The
  relay is the fallback for one that cannot.
- **The code lives in the chassis and only events-service turns it on**, at
  `POST https://events.${DOMAIN}/v1/auth/token`. A later External System that calls another service
  gets its token from the same URL. A second relay is never switched on.
- **Only `client_credentials`.** The body carries nothing else; no `grant_type`, `scope`,
  `username` or `password` is read. The secret travels only in the body: a request that carries it
  in the query string is refused with `400 TOKEN_SECRET_IN_URL`, so the leak is noticed and the
  secret rotated.
- **An allow list, `TokenRelay__AllowedClientIds`.** A client not on it gets the same
  `401 TOKEN_CLIENT_REJECTED` as a wrong secret, so the answer reveals no client id. An empty list
  turns the relay off and the route does not exist.
- **Errors are ours, success is Keycloak's.** `401 TOKEN_CLIENT_REJECTED`, `429 TOKEN_RATE_LIMITED`
  and `503 IDENTITY_UNAVAILABLE` go out through the chassis error mapper with their `code`.
- **Rate limited per client id (10 a minute) and per caller IP (30 a minute).** The IP is read
  through the forwarded headers, so the per-IP limit sees the caller and not Traefik.
- **One log line per call**: Information on success, Warning on a rejection or a rate limit, Error
  when Keycloak is down. It carries the client id, the outcome code, the caller IP and Keycloak's
  error word. Never the secret, never the token.
- **Tokens live the realm's default five minutes.** `client_credentials` issues no refresh token;
  the caller asks again on a `401`.
- **An External System is a confidential Keycloak client of its own**, one per environment, with an
  audience mapper for the service it calls. It never holds `platform:service` or `platform:staff`,
  so under ADR-0025 it reaches nothing until a route opens to one of its own narrow roles. Those
  roles arrive with the routes that need them.

## Consequences

- The relay's Keycloak call is a plain named `HttpClient`, not `AddServiceClient`: that client
  attaches events-service's own token, and the relay must forward the caller's credentials alone.
- Rate limiting is in memory per instance (ADR-0007). With one instance per environment the limits
  hold; a second instance would double them.
- The per-client limit can be spent by someone else. A client id is not a secret, and a call
  with a wrong secret still uses one of that client's ten permits, so anyone who knows
  `softexpert` can lock SoftExpert out for the rest of the minute with ten junk calls from ten
  IPs. Accepted: the lockout lasts a minute, the per-IP limit caps each attacker address, and
  each attempt leaves a Warning line with the caller IP. Counting only failed calls instead would
  keep the lockout and let a stolen secret run unthrottled.
- If SoftExpert, or a later External System, learns to speak OAuth2 it can call Keycloak directly,
  and the relay drops it from the allow list. Nothing else changes.
