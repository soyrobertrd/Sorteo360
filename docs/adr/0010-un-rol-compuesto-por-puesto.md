# ADR-0010 — One composite role per job, not per service

- Status: Accepted
- Date: 2026-07-27
- Refines: ADR-0004 (identity on Keycloak), whose role sketch predates the CRM

## Context

The realm has two layers. **Function roles** are leaf permissions named
`noun:verb` (`eventos:cerrar`, `companies:write`, `opts:write`); they are what the
code checks, one policy per role, applied with `.RequireAuthorization`.
**Composite roles** are what a person is actually granted, and Keycloak expands
them into function roles inside the token.

The composites were being created per service rather than per person. When the CRM
arrived it added `director_opt` and `admin_central` alongside the existing
`direccion_opt` and `admin`, so the seed user `direccion` held **two** composites
describing one job, and `admin.pgti360` held two more. `direccion_opt` and
`director_opt` also sat three characters apart while meaning different things,
which is how the problem surfaced during review.

Left alone this grows badly: every new service adds one more composite per job, and
granting someone a job means remembering the full set.

The platform runs only on developer machines today. No stage or production realm
exists, so there are no live user assignments to migrate and renaming costs nothing
but the repo edit.

## Decision

**One composite role per job, spanning every service that job touches.** Three of
them replace the previous five:

| Composite | Covers |
| --- | --- |
| `opt_coordinator` | Day-to-day operation: create and edit events, manage quote candidates, submit quotes and events for review, maintain suppliers, and add, update or delete CRM contacts |
| `opt_director` | Everything the coordinator does, plus the consequent decisions: cancel and close events, delete suppliers, and register companies |
| `central_admin` | Everything the director does, plus maintaining the OPT directory |

`gestor_opt`, `direccion_opt`, `admin`, `director_opt` and `admin_central` are gone.
Each seed user now holds exactly one composite.

**Function roles stay the stable layer.** They are referenced from code and are the
names to get right. Composite names live only in the realm file and expand inside
the token, so **renaming a composite is a realm edit plus a user reassignment, with
no code change**. The three names above are deliberately provisional: the client has
not confirmed their own titles, and when they do, only the realm file moves.

## Consequences

- **The `cotizaciones` function roles now guard something.** `cotizaciones:write`
  and `cotizaciones:someter` existed in the realm and in `AuthorizationPolicies`
  but no endpoint required them, so the whole quote surface fell through to the
  fallback policy: any authenticated user could add candidates, submit a quote, and
  set final amounts. They are now applied per endpoint. Without this the
  coordinator/director split is a description, not a control.
- **Approval permissions are deliberately absent.** The job description for the
  director includes approving submitted quotes and the final event budget, but
  ADR-0005 keeps approvals behind `IApprovalGate` as a stub, with
  `categoria_cotizacion.status` limited to `draft | submitted` and no approve or
  reject operation to guard. Inventing a role for an operation that does not exist
  would be a guess about the same client decision ADR-0005 is waiting on. The
  closest built behaviour, closing an event and materialising its cost, is
  `eventos:cerrar` and is already director-only.
- **`monto-final` is quote data, not budget approval.** It sits on a *candidato*,
  so it is `cotizaciones:write`. The event-level equivalent is `eventos:cerrar`.
- **The coordinator holds `contacts:delete`.** That is the right-to-erasure soft
  delete, granted at the lower tier by explicit choice rather than by inheritance.
  Revisit it if erasure ever needs a second pair of eyes.
- **No migration.** With no stage or production realm, importing the updated file
  into a fresh local Keycloak is the whole rollout. Once a realm exists anywhere
  else, a composite rename stops being free and needs the roles created and the
  users reassigned before the old ones are removed.
- Historical records keep the old names: ADR-0004's role sketch, the planning notes
  under `.scratch/`, and the demo script are point-in-time documents and are not
  rewritten.
