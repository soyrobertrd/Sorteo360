import type { ModuleKey } from '@/auth/roles'
import { useAuth } from '@/auth/AuthProvider'
import { isSwitchableModule } from '@/features/admin/featureCatalogue'
import { useFeatureSwitches } from '@/features/admin/hooks'

/** Los módulos que se abren desde el portal, en su orden. */
export const MODULOS_DEL_PORTAL = [
  'sorteo',
  'events',
  'convocatorias',
] as const satisfies readonly ModuleKey[]

/**
 * Los módulos del portal que este usuario ve: los que su rol abre y que no están apagados.
 * `listo` es falso mientras no llega la lectura de los interruptores.
 */
export function useModulosVisibles(): { modulos: ModuleKey[]; listo: boolean } {
  const { canAccessModule } = useAuth()
  const { isEnabled, isLoading } = useFeatureSwitches()
  const modulos = MODULOS_DEL_PORTAL.filter(
    (m) => canAccessModule(m) && (!isSwitchableModule(m) || (!isLoading && isEnabled(m))),
  )
  return { modulos, listo: !isLoading }
}

/** Con un solo módulo a la vista el portal sobra: se entra directo y no hay "volver". */
export function useSoloUnModulo(): boolean {
  const { modulos, listo } = useModulosVisibles()
  return listo && modulos.length === 1
}
