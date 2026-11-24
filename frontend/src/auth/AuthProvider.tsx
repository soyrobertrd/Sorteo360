import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react'
import { Loader2, ShieldAlert } from 'lucide-react'
import { getKeycloak, initKeycloak } from './keycloak'
import {
  currentMockIdentity,
  mockSessionActive,
  signOutMock,
  subscribeMockIdentity,
} from './mockIdentity'
import { LoginLocal } from './LoginLocal'
import { grantsModuleAccess, requiredRoleHint, type FunctionRole, type ModuleKey } from './roles'
import { setTokenProvider, setUnauthorizedHandler } from '@/api/http'
import { config } from '@/lib/config'

export interface AuthUser {
  /** Identificador del sujeto (claim `sub`): con él se compara quién sometió. */
  sub: string
  username: string
  nombre: string
  email?: string
  department?: string
  roles: string[]
}

interface AuthContextValue {
  /** null en modo mock (sin identidad) o mientras no hay sesión. */
  user: AuthUser | null
  hasRole: (role: FunctionRole) => boolean
  canAccessModule: (module: ModuleKey) => boolean
  logout: () => void
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  hasRole: () => true,
  canAccessModule: () => true,
  logout: () => {},
})

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  return useContext(AuthContext)
}

/** Permiso para una acción: `allowed` + tooltip cuando falta el rol. */
// eslint-disable-next-line react-refresh/only-export-components
export function useCan(role: FunctionRole): { allowed: boolean; title?: string } {
  const { hasRole } = useAuth()
  const allowed = hasRole(role)
  return allowed ? { allowed } : { allowed, title: requiredRoleHint(role) }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  // Modo local / mock: sesión simulada sin dependencia de Keycloak ni Docker,
  // con selector de perfil en desarrollo o autenticación directa local.
  if (config.apiMode !== 'http' || config.authMode !== 'keycloak') {
    return <MockProvider>{children}</MockProvider>
  }
  // Las páginas públicas las abre gente sin cuenta: pasar por el login de Keycloak las haría
  // inservibles, y en los enlaces del correo la redirección perdería el token del fragmento.
  if (isAnonymousPath(window.location.pathname)) {
    return <AuthContext.Provider value={ANONYMOUS}>{children}</AuthContext.Provider>
  }
  return <KeycloakProvider>{children}</KeycloakProvider>
}

/**
 * Las rutas que se abren sin login (la raíz `/` ya no: va directo al login): los textos legales
 * (`/terminos`, `/privacidad`), el registro `/crear-cuenta` y su
 * activación `/activar/:token`, todo `/publico/*` (cartelera, calendario, detalle, casos de
 * éxito y las tres páginas de los correos), el formulario suelto `/registro/:id` y el enlace de
 * completar datos de viático `/viatico/completar/:token`.
 */
function isAnonymousPath(pathname: string): boolean {
  return (
    pathname === '/crear-cuenta' ||
    pathname === '/terminos' ||
    pathname === '/privacidad' ||
    /^\/activar\/[^/]+\/?$/.test(pathname) ||
    /^\/publico(\/|$)/.test(pathname) ||
    /^\/registro\/[^/]+\/?$/.test(pathname) ||
    /^\/viatico\/completar\/[^/]+\/?$/.test(pathname)
  )
}

/**
 * Una visita que entró anónima por una ruta pública y navega a una del personal recarga la
 * página, para que esa ruta pase por el login de Keycloak como cualquier otra.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function requireLoginOutsidePublicPaths(pathname: string): void {
  if (config.apiMode !== 'http' || isAnonymousPath(pathname)) return
  if (isAnonymousPath(ENTRY_PATHNAME)) window.location.reload()
}

/** La ruta con la que se cargó la aplicación: la que decidió si hubo login. */
const ENTRY_PATHNAME = window.location.pathname

const ANONYMOUS: AuthContextValue = {
  user: null,
  hasRole: () => false,
  canAccessModule: () => false,
  logout: () => {},
}

function MockProvider({ children }: { children: ReactNode }) {
  const identity = useSyncExternalStore(subscribeMockIdentity, currentMockIdentity)
  const conSesion = useSyncExternalStore(subscribeMockIdentity, mockSessionActive)

  const value = useMemo<AuthContextValue>(
    () => ({
      user: {
        sub: identity.sub,
        username: identity.username,
        nombre: identity.nombre,
        email: identity.email,
        department: identity.department,
        roles: identity.roles,
      },
      hasRole: (role) => identity.roles.includes(role),
      canAccessModule: (module) => grantsModuleAccess(identity.roles, module),
      logout: config.mockLogin ? signOutMock : () => {},
    }),
    [identity],
  )

  // Como en producción: sin sesión, el login; las páginas públicas siguen abiertas.
  if (config.mockLogin && !conSesion && !isAnonymousPath(window.location.pathname)) {
    return <LoginLocal />
  }
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function KeycloakProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<'init' | 'ready' | 'error'>('init')

  useEffect(() => {
    let cancelled = false
    const keycloak = getKeycloak()

    initKeycloak()
      .then((authenticated) => {
        if (cancelled) return
        if (!authenticated) {
          // login-required ya redirigió; esto es solo un resguardo.
          keycloak.login()
          return
        }
        setTokenProvider(async () => {
          try {
            await keycloak.updateToken(30)
          } catch {
            await keycloak.login()
          }
          return keycloak.token ?? null
        })
        setUnauthorizedHandler(() => {
          void keycloak.login()
        })
        keycloak.onTokenExpired = () => {
          void keycloak.updateToken(30).catch(() => keycloak.login())
        }
        setStatus('ready')
      })
      .catch(() => {
        if (!cancelled) setStatus('error')
      })

    return () => {
      cancelled = true
    }
  }, [])

  const value = useMemo<AuthContextValue>(() => {
    const keycloak = getKeycloak()
    const parsed = (keycloak.tokenParsed ?? {}) as {
      sub?: string
      preferred_username?: string
      name?: string
      email?: string
      roles?: string[]
    }
    const roles = parsed.roles ?? []
    return {
      user:
        status === 'ready'
          ? {
              sub: parsed.sub ?? '',
              username: parsed.preferred_username ?? '—',
              nombre: parsed.name ?? parsed.preferred_username ?? '—',
              email: parsed.email,
              roles,
            }
          : null,
      // El token ya trae los permisos finos expandidos desde el rol compuesto,
      // así que se comparan directamente y ningún perfil salta la verificación.
      hasRole: (role) => roles.includes(role),
      canAccessModule: (module) => grantsModuleAccess(roles, module),
      logout: () => void keycloak.logout({ redirectUri: `${window.location.origin}/app` }),
    }
  }, [status])

  if (status === 'init') {
    return (
      <FullScreen>
        <Loader2 size={28} className="animate-spin text-primary-700" />
        <p className="text-sm text-slate-600">Conectando con el servicio de identidad…</p>
      </FullScreen>
    )
  }

  if (status === 'error') {
    return (
      <FullScreen>
        <ShieldAlert size={32} className="text-red-600" />
        <p className="max-w-sm text-center text-sm text-slate-700">
          No se pudo contactar el servicio de identidad configurado. Verifique la disponibilidad del
          proveedor de autenticación o cambie a modo local sin Keycloak.
        </p>
      </FullScreen>
    )
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function FullScreen({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-100">
      <p className="text-lg font-bold text-primary-800">Sorteo360</p>
      {children}
    </div>
  )
}
