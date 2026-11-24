# ADR-0016 — The realm JSON seeds a new realm; once it exists, the console owns it

- Status: Accepted
- Date: 2026-08-30
- Refines: ADR-0004 (identity on Keycloak), which checked the realm export in without saying
  what it governs outside a developer machine

## Context

`infra/keycloak/pgti360-realm.json` holds the whole authorization model: every role with
its composites (ADR-0010), the clients with their audience mappers, three test users, and,
since the login theme shipped, `loginTheme: pgti360`. ADR-0004 checked it in "for reproducible
local setup" and stopped there.

Three deployment shapes read it differently today:

| Stack | Command | Realm import |
| ----- | ------- | ------------ |
| `docker-compose.yml` (local) | `start-dev --import-realm` | mounted, imported on a fresh volume |
| `docker-compose.dokploy.yml` (stage) | `start --import-realm` | mounted, imported once, then hardened by hand |
| `docker-compose.stage.yml` / `.prod.yml` | `start` | not mounted at all |

**The import runs only when the realm does not already exist.** A restart, a redeploy, a new
image: none of them re-apply the file. So the JSON describes a realm at the moment of its
creation and nothing after that.

That is the opposite of how this platform treats database schema, where every service applies
its pending migrations at startup in every environment and the migrations are the single
authority. Reading the two rules as one is the mistake this ADR exists to prevent.

The login theme made the gap concrete. Adding `loginTheme: pgti360` to the JSON changes nothing
for any realm created before it, and the two registry compose files never read the JSON in the
first place. Both cases fail silently: the login page keeps rendering the factory theme
and no log line says why.

Two ways out were considered.

**Make the JSON authoritative everywhere**, with `keycloak-config-cli` or a partial-import job
on each boot. It fails on what the file contains. The three users carry the password `localdev`,
and `pgti360-app` carries a `http://localhost:*` redirect wildcard with `localhost` web origins.
Only the `events-service` secret is already parameterized, as `${EVENTS_SERVICE_CLIENT_SECRET}`.
Re-asserting that file against production would undo the hardening list in the deploy
runbook every time the stack came up. The runbook deliberately diverges from the
file: it resets those passwords, switches direct access grants off, and narrows the redirect
URIs. Making the file win means splitting the development seed out of it first, which is real
work and buys nothing until a second non-local realm exists.

**Say plainly which side owns what**, and write down the two-place edit that follows. That is
cheaper and it is what the code and the runbook already do.

## Decision

- **The realm JSON is authoritative for exactly one act: creating the `pgti360` realm the first
  time, in an environment that mounts it.** From the first successful boot onward the running
  realm is the authority, administered in the Keycloak console. Nothing re-applies the file.
- **The file is a development seed, not a deployment artifact.** It keeps the test users, their
  `localdev` passwords, and the localhost redirect wildcard and web origins. Stage and
  production deployed from the registry files never mount it. Stage deployed through Dokploy
  imports it because building every role and audience mapper by hand costs hours and
  a forgotten mapper surfaces as an undiagnosable 401, and it pays for that shortcut with the
  hardening list applied immediately after (`docs/deploy-with-dokploy.md`, §6).
- **A realm change lands in two places in the same PR**: the JSON, so every environment created
  from here on has it, and a runbook step, so an environment that already exists gets it by hand.
  Either one alone leaves half the estate behind. The step names the console path and a way to
  verify it, the way the login theme section does with its `curl` on the authorization endpoint.
- **Divergence between the file and a live realm is expected, and is not drift.** Passwords,
  redirect URIs, web origins, direct access grants and the client secret are supposed to differ
  in every environment that is not a laptop. Do not build a checker that diffs the whole file
  against a running realm; it would report the hardening as a defect.
- **Role names and client ids in the JSON are the contract the code is written against**
  (ADR-0010). Renaming one is a platform-wide change: the JSON, every policy that names it, and
  every live realm, by hand.
- **No realm configuration tool is adopted now**, neither `keycloak-config-cli` nor the Terraform
  provider. Reconsider when a second non-local realm exists, or when someone other than the
  person who wrote the file administers one. The prerequisite for either is splitting the
  development seed out of the JSON, not choosing between the tools.

## Consequences

- Every realm change needs a runbook line, and writing that line is part of the change. A PR that
  edits `pgti360-realm.json` and touches no runbook is incomplete, and the review should say so.
- A realm created before a change stays behind until someone applies it, and nothing detects that.
  Verification is per change and manual. The login theme's `href` check is the model: one command,
  one observable answer.
- The Dokploy stack keeps `--import-realm`, and it stays paired with the hardening list. Removing
  the import to avoid shipping `localdev` credentials would trade a documented, minutes-long
  exposure for hours of hand-built mappers.
- Services are unaffected by which side put a role in the realm. They see it through the token and
  their `Keycloak__Authority` / `Keycloak__Audience` variables, never by reading the file, so this
  decision changes no service code and no test.
- The realm's own database is what actually persists this state. `docker compose down -v` wipes it
  locally and the next `up` re-imports from the file, which is why local is the one environment
  where the JSON always describes the realm accurately.
