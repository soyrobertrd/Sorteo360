import { LayoutGrid, Settings2, ToggleRight, type LucideIcon } from 'lucide-react'
import { ROLES, type FunctionRole } from '@/auth/roles'
import {
  moduleForPath,
  sidebarEntries,
  type CatalogueModule,
  type SwitchKey,
} from './featureCatalogue'

/** Una entrada de la barra lateral y del menú móvil. */
export interface SidebarEntry {
  to: string
  label: string
  icon: LucideIcon
  /** El interruptor que la esconde; una entrada sin clave no se apaga. */
  key?: SwitchKey
  /** Solo sale con este rol. */
  role?: FunctionRole
}

/**
 * Lo que dibujan la barra de escritorio y el menú móvil: un título, la entrada de inicio (que
 * abre su ruta exacta y nunca se esconde) y las demás entradas en su orden.
 */
export interface SidebarMenu {
  heading: string
  dashboard: SidebarEntry
  entries: readonly SidebarEntry[]
}

/** La barra de un módulo del catálogo. */
export function menuOf(module: CatalogueModule): SidebarMenu {
  return {
    heading: module.sidebarHeading,
    dashboard: { to: module.route, label: 'Dashboard', icon: LayoutGrid },
    entries: sidebarEntries(module).map((feature) => ({
      to: feature.route,
      label: feature.label,
      icon: feature.icon,
      key: feature.key as SwitchKey,
      role: feature.role,
    })),
  }
}

/**
 * Administración no es un módulo del catálogo: no se apaga, y la cuida su rol en el ruteador.
 * Cada entrada sale solo con el rol de su herramienta.
 */
export const ADMIN_MENU: SidebarMenu = {
  heading: 'Administración',
  dashboard: { to: '/admin', label: 'Herramientas', icon: Settings2 },
  entries: [
    {
      to: '/admin/feature-switches',
      label: 'Interruptores de funciones',
      icon: ToggleRight,
      role: ROLES.featureSwitchesWrite,
    },
  ],
}

/** El menú de la ruta abierta. */
export function menuForPath(pathname: string): SidebarMenu {
  if (pathname === '/admin' || pathname.startsWith('/admin/')) return ADMIN_MENU
  return menuOf(moduleForPath(pathname))
}
