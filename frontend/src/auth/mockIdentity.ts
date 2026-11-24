import { COMPOSITE_GRANTS, type CompositeRole } from './roles'

export interface MockIdentity {
  sub: string
  nombre: string
  username: string
  email: string
  department: string
  composite: CompositeRole
  /** El compuesto más sus permisos finos, tal como llegarían en el token. */
  roles: string[]
}

type MockPersona = Omit<MockIdentity, 'roles'>

/**
 * Las personas del realm (v1.0: admin, supervisor y sorteo). Los permisos no se listan a
 * mano: se expanden del compuesto igual que hace Keycloak al emitir el token, para
 * que el modo mock y el modo http gaten exactamente lo mismo.
 *
 * La primera es la identidad por defecto, y es Administración General porque es la
 * única que alcanza todos los módulos: desde ahí se puede bajar a Supervisor o
 * Sorteo para mostrar qué se bloquea.
 */
const PERSONAS: MockPersona[] = [
  {
    sub: 'u-admin',
    nombre: 'María Fernández',
    username: 'admin',
    email: 'maria.fernandez@empresa.com',
    department: 'Administración General',
    composite: 'central_admin',
  },
  {
    sub: 'u-dir',
    nombre: 'Luis Ortega',
    username: 'supervisor',
    email: 'luis.ortega@empresa.com',
    department: 'Dirección / Talento Humano',
    composite: 'opt_director',
  },
  {
    sub: 'u-sorteo',
    nombre: 'Ana Reyes',
    username: 'sorteo',
    email: 'ana.reyes@empresa.com',
    department: 'Operación del sorteo',
    composite: 'proyector',
  },
]

export const MOCK_IDENTITIES: MockIdentity[] = PERSONAS.map((persona) => ({
  ...persona,
  roles: [persona.composite, ...COMPOSITE_GRANTS[persona.composite]],
}))

let current = MOCK_IDENTITIES[0]
const listeners = new Set<() => void>()

export function currentMockIdentity(): MockIdentity {
  return current
}

export function setMockIdentity(sub: string): void {
  const next = MOCK_IDENTITIES.find((i) => i.sub === sub)
  if (!next || next === current) return
  current = next
  for (const listener of listeners) listener()
}

export function subscribeMockIdentity(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

// ---------- Sesión del modo demostración ----------
// Sin Keycloak no hay login real: esta sesión solo hace que la demo se comporte como producción
// (primero el login, después /app). La contraseña es la de los usuarios sembrados en el realm.
// No protege nada: los datos del modo mock viven en el navegador.

const SESION = 'sorteo360.mock.sesion'
export const CLAVE_DEMO = 'localdev'

function leerSesion(): boolean {
  try {
    const sub = sessionStorage.getItem(SESION)
    const guardada = MOCK_IDENTITIES.find((i) => i.sub === sub)
    if (guardada) current = guardada
    return guardada != null
  } catch {
    return false
  }
}

let sesionActiva = leerSesion()

export function mockSessionActive(): boolean {
  return sesionActiva
}

/** Entra con el usuario del realm (`admin`, `supervisor`, `sorteo`); `false` si no coincide. */
export function signInMock(username: string, password: string): boolean {
  const identity = MOCK_IDENTITIES.find(
    (i) =>
      i.username === username.trim().toLowerCase() || i.email === username.trim().toLowerCase(),
  )
  if (!identity || password !== CLAVE_DEMO) return false
  current = identity
  sesionActiva = true
  try {
    sessionStorage.setItem(SESION, identity.sub)
  } catch {
    // Sin almacenamiento la sesión dura hasta recargar.
  }
  for (const listener of listeners) listener()
  return true
}

export function signOutMock(): void {
  sesionActiva = false
  try {
    sessionStorage.removeItem(SESION)
  } catch {
    // Nada que borrar.
  }
  for (const listener of listeners) listener()
}
