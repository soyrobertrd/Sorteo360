import { describe, expect, it } from 'vitest'
import { COMPOSITE_GRANTS, grantsModuleAccess, landsOnScanner, type CompositeRole } from './roles'

// El espejo de `infra/keycloak/sorteo360-realm.json`: los tres perfiles de personal llevan
// `platform:staff`, la puerta no (ADR-0025).
describe('platform:staff', () => {
  it.each(['opt_coordinator', 'opt_director', 'central_admin'] as const)(
    'lo concede %s',
    (composite) => {
      expect(COMPOSITE_GRANTS[composite]).toContain('platform:staff')
    },
  )

  it('no lo concede event_staff', () => {
    expect(COMPOSITE_GRANTS.event_staff).not.toContain('platform:staff')
  })
})

/** Los roles que trae el token de un perfil, tal como los expande la identidad simulada. */
function tokenRoles(composite: CompositeRole): string[] {
  return [composite, ...COMPOSITE_GRANTS[composite]]
}

// La puerta solo alcanza /scanner: Convocatorias lee la lista de eventos del personal, que
// event_staff no puede leer (ADR-0025, ).
describe('módulo Convocatorias', () => {
  it.each(['opt_coordinator', 'opt_director', 'central_admin'] as const)(
    'lo abre %s',
    (composite) => {
      expect(grantsModuleAccess(tokenRoles(composite), 'convocatorias')).toBe(true)
    },
  )

  it('no lo abre event_staff, que entra directo al escáner', () => {
    const roles = tokenRoles('event_staff')
    expect(grantsModuleAccess(roles, 'convocatorias')).toBe(false)
    expect(landsOnScanner(roles)).toBe(true)
  })
})
