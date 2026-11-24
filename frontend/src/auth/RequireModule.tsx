import { Link, Navigate, Outlet } from 'react-router-dom'
import { ShieldAlert } from 'lucide-react'
import { useAuth } from './AuthProvider'
import {
  landsOnScanner,
  moduleAccessHint,
  requiredRoleHint,
  type FunctionRole,
  type ModuleKey,
} from './roles'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Feedback'
import { SwitchedOffNotice } from '@/features/admin/FeatureGate'
import { CATALOGUE_BY_KEY, isSwitchableModule } from '@/features/admin/featureCatalogue'
import { useFeatureSwitches } from '@/features/admin/hooks'

/**
 * Guard de ruta: solo deja pasar a `<Outlet />` si el usuario activo tiene acceso al módulo y el
 * módulo no está apagado (docs/adr/0039). El personal de la puerta no tiene módulos: cualquier
 * enlace a uno lo lleva al escáner. Mientras llega la primera lectura de los interruptores espera,
 * para que un enlace directo a un módulo apagado no abra la pantalla ni pida sus datos.
 */
export function RequireModule({ module }: { module: ModuleKey }) {
  const { canAccessModule, user } = useAuth()
  const { isEnabled, isLoading } = useFeatureSwitches()
  if (user && landsOnScanner(user.roles)) return <Navigate to="/scanner" replace />
  if (!canAccessModule(module)) {
    return (
      <AccessDenied
        reason={`Tu perfil activo no tiene acceso a este módulo. ${moduleAccessHint(module)}.`}
      />
    )
  }
  if (!isSwitchableModule(module)) return <Outlet />
  if (isLoading) return <Spinner />
  if (isEnabled(module)) return <Outlet />
  return <SwitchedOffNotice module={CATALOGUE_BY_KEY[module]} />
}

/**
 * Guard de ruta para una pantalla suelta, fuera de un módulo: pide un permiso fino, o cualquiera
 * de varios cuando la pantalla reúne herramientas de roles distintos (el hub de Administración).
 */
export function RequireRole({ role }: { role: FunctionRole | readonly FunctionRole[] }) {
  const { hasRole } = useAuth()
  const roles: readonly FunctionRole[] = typeof role === 'string' ? [role] : role
  if (roles.some((r) => hasRole(r))) return <Outlet />

  return (
    <AccessDenied
      reason={`Tu perfil activo no tiene acceso a esta pantalla. ${requiredRoleHint(roles[0])}.`}
    />
  )
}

function AccessDenied({ reason }: { reason: string }) {
  return (
    <div className="flex min-h-[70vh] flex-col items-center justify-center gap-5 text-center px-4">
      <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-rose-50 text-rose-700 border border-rose-200 dark:bg-rose-950/50 dark:text-rose-300 dark:border-rose-900">
        <ShieldAlert size={30} />
      </div>
      <div className="space-y-1.5">
        <h1 className="font-sans text-xl font-black text-slate-900 dark:text-white">
          Acceso restringido
        </h1>
        <p className="max-w-sm text-xs text-slate-500 dark:text-slate-400">{reason}</p>
      </div>
      <Link to="/app">
        <Button className="cursor-pointer bg-brand-deep font-bold text-white hover:bg-brand-deep/90">
          Volver al portal
        </Button>
      </Link>
    </div>
  )
}
