import type { ReactNode } from 'react'
import { Link, Outlet } from 'react-router-dom'
import { PowerOff } from 'lucide-react'
import { useAuth } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Feedback'
import { NotFoundPage } from '@/features/errors/NotFoundPage'
import {
  CATALOGUE_BY_KEY,
  FEATURE_BY_KEY,
  moduleOf,
  type CatalogueModule,
  type FeatureKey,
} from './featureCatalogue'
import { useFeatureSwitches, usePublicPortalOpen } from './hooks'

/**
 * Muestra lo que envuelve solo si su función y su módulo están encendidos; si no, nada. Envuelve
 * un enlace a una función fuera de la barra lateral, como los accesos de un Dashboard.
 */
export function FeatureGate({
  featureKey,
  children,
}: {
  featureKey: FeatureKey
  children: ReactNode
}) {
  return useFeatureSwitches().isEnabled(featureKey) ? <>{children}</> : null
}

/**
 * Protege las rutas de una función, como `RequireModule` las de un módulo, que ya revisó el
 * interruptor del módulo: deja pasar a `<Outlet />` si la función está encendida y, si no, responde
 * como `SwitchedOffNotice`. Cubre también las rutas de detalle. Mientras llega la primera lectura espera,
 * para que un enlace directo a algo apagado no abra la pantalla ni pida sus datos.
 */
export function RequireFeature({ featureKey }: { featureKey: FeatureKey }) {
  const { isEnabled, isLoading } = useFeatureSwitches()
  if (isLoading) return <Spinner />
  if (isEnabled(featureKey)) return <Outlet />
  return (
    <SwitchedOffNotice
      module={CATALOGUE_BY_KEY[moduleOf(featureKey)]}
      featureLabel={FEATURE_BY_KEY.get(featureKey)?.label}
    />
  )
}

/**
 * Lo que ve quien llega a algo apagado por un enlace directo. Solo quien enciende y apaga los
 * interruptores sabe que está apagado: un módulo entero lo manda al portal, y una función, al
 * Dashboard de su módulo. Para todos los demás lo apagado no existe, así que ven la misma página
 * 404 que cualquier enlace roto (Robert, PR #517).
 */
export function SwitchedOffNotice({
  module,
  featureLabel,
}: {
  module: CatalogueModule
  featureLabel?: string
}) {
  const { hasRole } = useAuth()
  if (!hasRole(ROLES.featureSwitchesWrite)) return <NotFoundPage />

  return (
    <div className="flex min-h-[70vh] flex-col items-center justify-center gap-5 px-4 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-2xl border border-slate-200 bg-slate-100 text-slate-500 dark:border-slate-700 dark:bg-slate-800">
        <PowerOff size={30} />
      </div>
      <div className="space-y-1.5">
        <h1 className="font-sans text-xl font-black text-slate-900 dark:text-white">
          {featureLabel ? 'Función desactivada' : 'Módulo desactivado'}
        </h1>
        <p className="max-w-sm text-xs text-slate-500 dark:text-slate-400">
          {featureLabel ? `${module.label} › ${featureLabel}` : module.label} no está disponible en
          este momento.
        </p>
      </div>
      <div className="flex items-center gap-3">
        <Link to="/admin/feature-switches">
          <Button type="button" variant="outline" className="cursor-pointer font-bold">
            Ver interruptores
          </Button>
        </Link>
        <Link to={featureLabel ? module.route : '/'}>
          <Button
            type="button"
            className="cursor-pointer bg-brand-deep font-bold text-white hover:bg-brand-deep/90"
          >
            {featureLabel ? `Ir a ${module.label}` : 'Volver al portal'}
          </Button>
        </Link>
      </div>
    </div>
  )
}

/** Las rutas del portal público y del registro: solo con convocatorias encendido; si no, 404. */
export function RequirePublicPortal() {
  const { open, isLoading } = usePublicPortalOpen()
  if (isLoading) return <Spinner />
  return open ? <Outlet /> : <NotFoundPage />
}
