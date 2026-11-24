# Roles catalogue

Every Keycloak realm role the platform's code knows about: what it lets a caller do, which service
checks it, who holds it, and which ADR decided it. It is for developers adding a role and for
agents asking which role guards what.

**The source is `infra/keycloak/sorteo360-realm.json`, not a live realm.** The JSON seeds the `sorteo360`
realm and the console owns it afterwards.

`scripts/roles-parity.sh` fails when this page, `infra/keycloak/sorteo360-realm.json` and the
services' `AuthorizationPolicies.cs` disagree on a role's name, kind, composites or service accounts.
The section `## Planned` is ignored by the parity check.

## Realm roles

| Role | Kind | Owner service | What it lets you do | Granted by job | Service accounts holding it | Decided in |
| --- | --- | --- | --- | --- | --- | --- |
| `events:write` | realm role | events-service | Create and edit events | `opt_coordinator` `opt_director` `central_admin` `account_admin` | none | none |
| `events:start` | realm role | events-service | Start an event | `opt_coordinator` `opt_director` `central_admin` `account_admin` | none | none |
| `events:cancel` | realm role | events-service | Cancel an event with reason | `opt_director` `central_admin` `account_admin` | none | none |
| `events:close` | realm role | events-service | Close an event | `opt_director` `central_admin` `account_admin` | none | none |
| `raffles:write` | realm role | events-service | Configure raffles, rounds, prizes, participants and branding | `opt_coordinator` `opt_director` `central_admin` `account_admin` | none | none |
| `raffles:draw` | realm role | events-service | Start, draw, void, hand over prizes and issue notarial records | `opt_coordinator` `opt_director` `central_admin` `proyector` `account_admin` | none | none |
| `feature-switches:write` | realm role | events-service | Enable or disable Sorteo360 modules and feature switches | `central_admin` | none | none |
| `platform-admin` | realm role | events-service | Approve or reject account plan transfer requests | `central_admin` | none | none |
| `campaigns:write` | realm role | convocatorias-service | Build invitation campaigns | `opt_coordinator` `opt_director` `central_admin` `account_admin` | none | none |
| `campaigns:launch` | realm role | convocatorias-service | Launch an invitation campaign | `opt_coordinator` `opt_director` `central_admin` `account_admin` | none | none |
| `convocatorias:read` | realm role | convocatorias-service | Read campaign tracking | `opt_coordinator` `opt_director` `central_admin` `account_admin` | none | none |
| `check-in:write` | realm role | convocatorias-service | Scan QR check-ins and undo check-ins | `opt_coordinator` `opt_director` `central_admin` `event_staff` `account_admin` | none | none |
| `check-in:read` | realm role | convocatorias-service | Read door check-in list without emails; also read door events on `events-service` | `opt_coordinator` `opt_director` `central_admin` `event_staff` `account_admin` | none | none |
| `check-in:service-read` | service-only realm role | convocatorias-service | Read full check-in list from `events-service` | none | `events-service` | none |
| `notifications:send` | service-only realm role | notification-service | Send emails through `notification-service` | none | `events-service` `convocatorias-service` | none |
| `notifications:read` | realm role | notification-service | Read notification send history | `central_admin` | `convocatorias-service` | none |
| `platform:staff` | realm role | all three | Base staff fallback role across routes without an explicit policy | `opt_coordinator` `opt_director` `central_admin` `proyector` `account_admin` | none | none |
| `platform:service` | service-only realm role | all three | Base service-account fallback role across routes without an explicit policy | none | `events-service` `convocatorias-service` | none |

## Realm composites

| Role | Kind | Job | Decided in |
| --- | --- | --- | --- |
| `opt_coordinator` | realm composite | Event & raffle operator: configures events, convocatorias, participant rosters, door check-in and raffles | none |
| `opt_director` | realm composite | Supervisor: everything an operator does plus cancelling/closing events | none |
| `central_admin` | realm composite | Administrator: everything a supervisor does plus feature switches and notification history | none |
| `event_staff` | realm composite | Door staff: QR check-in at event doors only (excludes `platform:staff`) | none |
| `proyector` | realm composite | Projector display: runs/projects live raffle view without configuration permissions | none |
| `account_admin` | realm composite | Account administrator: full access to events, convocatorias, raffles, check-in, branding and account plan within their account | none |

## Planned

Roles tracked outside the active realm JSON (ignored by `scripts/roles-parity.sh`):

None today.
