# ADR-0025 — The fallback policy requires a staff or service role

- Status: Accepted
- Date: 2026-09-26
- Follows from: ADR-0010 (one composite role per job), ADR-0022 (door staff check people in for the
  raffle)

## Context

Every service sets its fallback authorization policy to `RequireAuthenticatedUser()` and guards
writes with one `RequireRole` policy per function role. Reads were left on the fallback on purpose:
until now, everybody who could sign in was MITUR staff, so "signed in" and "allowed to read" meant
the same thing.

Check-in breaks that (`docs/specs/convocatorias/09-check-in.md` §8). The person at an Event's door
needs a login, and that login must not be an `opt_coordinator`. A new composite, `event_staff`, holds
two check-in roles and nothing else. But an `event_staff` token is a signed-in user, and in
events-service and crm-service that is all most endpoints ask for. An audit on 2026-09-26 found:

- **crm-service:** every contact (names, emails, phones, suppression reasons), every company, the OPT
  directory, and create, edit and delete on tasks, all on the fallback.
- **events-service:** events, quotes, service suppliers with their contacts' details, and
  `GET /v1/files/{id}/content`, which serves the bytes of every stored file. Once the raffle ships,
  that includes winners' identity-document numbers and signatures (`raffles/03` §7).

convocatorias-service and notification-service have no such gap today: every non-public route
there carries an explicit role.

Service accounts are signed-in callers too, and several of them use fallback-only routes:
convocatorias-service reads `GET /v1/events` and `GET /v1/events/{id}` and reads CRM contacts;
events-service reads `GET /v1/companies/{id}` and `GET /v1/opts`.

## Decision

**In all four services, the fallback policy requires the realm role `platform:staff` or the realm
role `platform:service`.**

- **`platform:staff`** is held by the three staff composites: `opt_coordinator`, `opt_director` and
  `central_admin`. A composite created later decides deliberately whether it carries it.
- **`platform:service`** is assigned directly to each service account: events-service's and
  convocatorias-service's today, the only two the realm has. A service that gains an account later
  gets it as part of that change.
  **Amended 2026-10-09 (PGTI-126):** the realm has three accounts, not two. crm-service's
  (`service-account-crm-service`, PGTI-156) arrived after this ADR was written and gets
  `platform:service` too, although today it calls only `notifications:send`, which has an explicit
  policy.
- **`event_staff` holds neither.** Everything it reaches, it reaches through an explicit policy.
- **No endpoint changes.** Every read that works for staff today keeps working, and every
  service-to-service call keeps working, with no route edited.

Both roles land in the realm JSON and as a console step in `docs/deploy-with-dokploy.md`, per
ADR-0016, in the same PR as the policy change. **The console step goes first on a running realm**:
if the policy ships before the roles exist, every read in the service answers `403`.

## Alternatives rejected

- **A module read role per service** (`events:read`, `crm:read`, later `raffles:read`), put on every
  currently open endpoint. It is tidier per route, but it touches about 30 of them now, and every
  future endpoint has to remember its role. Forgetting one fails open, which is the failure this ADR
  exists to prevent.
- **One role, `platform:member`,** on the staff composites and the service accounts alike. It is one
  role fewer, but a realm where door staff are not "members" reads as if they were not part of the
  platform, and whoever next opens the console will "fix" it.
- **Explicit service policies on the routes services call**, the way `crm:service-read` guards
  `GET /v1/companies/directory`. About six routes today, and it breaks silently the day a service
  starts calling a new one.
- **Defer, and do not ship `event_staff` yet.** The door would then need an `opt_coordinator` login,
  which can create Events and delete CRM contacts. That is the outcome check-in's role design exists
  to avoid.

## Consequences

- **A new endpoint is shut to door staff by default.** Forgetting a policy now fails closed for them
  and changes nothing for staff.
- **Tests do not catch a missing realm role.** `TestAuthHandler` grants whatever roles a test asks
  for, so a service account without `platform:service` passes every local check and fails on deploy.
  The change ships with integration tests in each service asserting that a token holding only
  `platform:service` reaches each route another service calls, and that a token holding only
  `event_staff` gets `403` from them.
- **The SPA must stop calling what a user cannot read.** `AppShell` loads pending quotes and events
  near closing for every user; for door staff those calls now answer `403` instead of succeeding
  quietly. `09` §9 lists it as a frontend change request.
- **`/_alive`, `/_ready` and the public convocatorias routes are untouched.** They are
  `AllowAnonymous`, which the fallback never governed.
