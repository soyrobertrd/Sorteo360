# ADR-0004 — Identity is Keycloak on-premises; remove the Azure AD wiring

- Status: Accepted
- Date: 2026-07-01
- Refined by: ADR-0010 (one composite role per job, replacing this ADR's role sketch)
- Refined by: ADR-0016 (the checked-in realm export seeds a new realm; the console owns it after that)

## Context
The platform will run on-premises, not in the cloud. `eventos-service`
currently wires Azure AD (`Microsoft.Identity.Web`,
`AddMicrosoftIdentityWebApiAuthentication`) and the service `CLAUDE.md`
documents Azure AD / Azure Redis conventions — assumptions inherited from the
service template. Meanwhile endpoints stamp `Guid.Empty` as
`created_by_id`/`updated_by_id` because no real identity flows in.

## Decision
- **Keycloak** (self-hosted, in the Compose stack) is the identity provider, as
  the v12 design assumed: services validate its JWTs; role claims drive
  authorization policies (gestor_opt, direccion_opt, comite, finanzas,
  reportes, admin — final role set to be confirmed).
- The Azure AD wiring is **removed**, not kept as a fallback. Azure-specific
  conventions in the service docs (Azure AD auth, Azure Redis managed identity)
  are rewritten when this lands.
- Actor stamping (`created_by_id`/`updated_by_id`) is wired from the validated
  token at the same time; `Guid.Empty` disappears with it.

## Consequences
- A Keycloak container joins `docker-compose.yml` (no profile — infra), with
  realm export checked in for reproducible local setup.
- Integration tests keep the test authentication scheme; nothing there depends
  on the provider.
- Any future gateway (v12's YARP) validates the same Keycloak JWTs; this ADR
  does not commit to the gateway itself.
