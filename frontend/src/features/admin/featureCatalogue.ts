import {
  BarChart3,
  Calendar,
  CalendarDays,
  FileText,
  List,
  QrCode,
  Send,
  Sparkles,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { type FunctionRole } from '@/auth/roles'

/**
 * El catálogo de lo que se puede apagar: cada módulo habilitado en la plataforma y las funciones
 * de su barra lateral.
 *
 * Es también la barra lateral: el menú de escritorio y el móvil de `AppShell.tsx` se dibujan desde
 * aquí, en este orden. El Dashboard de cada módulo no es una función y no tiene interruptor:
 * apagarlo sería apagar el módulo.
 */
export const FEATURE_CATALOGUE = [
  {
    key: 'sorteo',
    label: 'Rifas en vivo',
    sidebarHeading: 'Rifas en vivo',
    icon: Sparkles,
    route: '/sorteo',
    features: [
      { key: 'sorteo.raffles', label: 'Sorteos', icon: List, route: '/sorteo/sorteos' },
      {
        key: 'sorteo.winners-history',
        label: 'Historial de Ganadores',
        icon: BarChart3,
        route: '/sorteo/historial',
      },
    ],
  },
  {
    key: 'events',
    label: 'Agenda de eventos',
    sidebarHeading: 'Agenda de eventos',
    icon: Calendar,
    route: '/eventos',
    features: [
      { key: 'events.events', label: 'Eventos', icon: Calendar, route: '/eventos/todos' },
      {
        key: 'events.calendar',
        label: 'Calendario',
        icon: CalendarDays,
        route: '/eventos/calendario',
      },
    ],
  },
  {
    key: 'convocatorias',
    label: 'Invitaciones',
    sidebarHeading: 'Invitaciones',
    icon: FileText,
    route: '/convocatorias',
    features: [
      {
        key: 'convocatorias.events',
        label: 'Eventos',
        icon: CalendarDays,
        route: '/convocatorias/eventos',
      },
      {
        key: 'convocatorias.campaigns',
        label: 'Campañas',
        icon: Send,
        route: '/convocatorias/campanas',
      },
      {
        key: 'convocatorias.templates',
        label: 'Plantillas',
        icon: FileText,
        route: '/convocatorias/plantillas',
      },
      {
        key: 'convocatorias.guests',
        label: 'Invitados',
        icon: Users,
        route: '/convocatorias/invitados',
      },
      {
        key: 'convocatorias.check-in',
        label: 'Check-in',
        icon: QrCode,
        route: '/convocatorias/checkin',
      },
    ],
  },
] as const satisfies readonly CatalogueModule[]

export interface CatalogueFeature {
  key: string
  label: string
  icon: LucideIcon
  route: string
  /** La entrada solo sale con este rol; la vista previa la marca "según rol". */
  role?: FunctionRole
}

export interface CatalogueModule {
  key: string
  label: string
  /** El título de su barra lateral, en escritorio y en el menú móvil. */
  sidebarHeading: string
  icon: LucideIcon
  route: string
  /** Otras rutas, fuera de `route`, que también son de este módulo. */
  aliases?: readonly string[]
  features: readonly CatalogueFeature[]
  /**
   * Las entradas de la barra que llevan a una función de otro módulo. No tienen interruptor propio:
   * se esconden con el de su destino, y `after` dice detrás de qué función salen.
   */
  crossLinks?: readonly { key: string; after: string }[]
}

type Catalogue = typeof FEATURE_CATALOGUE

/** Un módulo que se puede apagar; es un `ModuleKey` de `roles.ts`. */
export type SwitchableModuleKey = Catalogue[number]['key']

/** La clave de una función: siempre la de su módulo, un punto y la suya. */
export type FeatureKey = Catalogue[number]['features'][number]['key']

/** Toda clave de interruptor: la de un módulo o la de una de sus funciones. */
export type SwitchKey = SwitchableModuleKey | FeatureKey

/** Cada módulo por su clave. */
export const CATALOGUE_BY_KEY = Object.fromEntries(
  FEATURE_CATALOGUE.map((module): [string, CatalogueModule] => [module.key, module]),
) as Record<SwitchableModuleKey, CatalogueModule>

/** Cada función por su clave, de todos los módulos. */
export const FEATURE_BY_KEY: ReadonlyMap<string, CatalogueFeature> = new Map(
  FEATURE_CATALOGUE.flatMap((module): readonly CatalogueFeature[] => module.features).map(
    (feature) => [feature.key, feature],
  ),
)

/** El módulo de una clave: `crm` para `crm.companies`, y la clave misma para un módulo. */
export const moduleOf = (key: SwitchKey): SwitchableModuleKey =>
  key.split('.')[0] as SwitchableModuleKey

/** Si un `ModuleKey` del portal tiene interruptor: los "Próximamente" no lo tienen. */
export const isSwitchableModule = (module: string): module is SwitchableModuleKey =>
  Object.hasOwn(CATALOGUE_BY_KEY, module)

/**
 * El módulo de una ruta, por su `route` o uno de sus `aliases`. Gestión de Eventos responde por
 * todo lo que no es de otro módulo, como hacía la barra antes del catálogo.
 */
export function moduleForPath(pathname: string): CatalogueModule {
  const owns = (prefix: string) => pathname === prefix || pathname.startsWith(`${prefix}/`)
  return (
    FEATURE_CATALOGUE.find((module) =>
      [module.route, ...((module as CatalogueModule).aliases ?? [])].some(owns),
    ) ?? CATALOGUE_BY_KEY.sorteo
  )
}

/** Las entradas de la barra del módulo en su orden, con los enlaces a otro módulo en su lugar. */
export function sidebarEntries(module: CatalogueModule): CatalogueFeature[] {
  return module.features.flatMap((feature) => [
    feature,
    ...(module.crossLinks ?? [])
      .filter((link) => link.after === feature.key)
      .flatMap((link) => FEATURE_BY_KEY.get(link.key) ?? []),
  ])
}
