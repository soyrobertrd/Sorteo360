import { useAuth } from '@/auth/AuthProvider'
import { useFeatureSwitches } from '@/features/admin/hooks'
import type { SidebarEntry, SidebarMenu } from '@/features/admin/sidebarMenus'

/** Hasta cinco destinos caben en la barra inferior del teléfono; más, van al menú desplegable. */
const MAX_PESTANAS = 5

/** Las entradas que este usuario ve: las de su rol y con su interruptor encendido. */
export function useVisibleEntries(menu: SidebarMenu): readonly SidebarEntry[] {
  const { hasRole } = useAuth()
  const { isEnabled } = useFeatureSwitches()
  return menu.entries.filter(
    (entry) => (!entry.role || hasRole(entry.role)) && (!entry.key || isEnabled(entry.key)),
  )
}

/** Las entradas del menú en el teléfono: la de inicio y las visibles. */
export function useMobileEntries(menu: SidebarMenu): readonly SidebarEntry[] {
  return [menu.dashboard, ...useVisibleEntries(menu)]
}

/** Si el menú cabe en la barra inferior del teléfono. */
export function useCabeEnPestanas(menu: SidebarMenu): boolean {
  return useMobileEntries(menu).length <= MAX_PESTANAS
}
