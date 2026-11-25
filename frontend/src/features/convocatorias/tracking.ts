import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import { useCan } from '@/auth/AuthProvider'
import { useFeatureSwitches } from '@/features/admin/hooks'
import { ROLES } from '@/auth/roles'
import { publicEventoKeys } from '@/features/publico/hooks'
import type { CampaignResponse } from '@/api/types'
import { useCampaigns } from './campanas'

/**
 * Qué eventos tienen convocatoria y cuánta gente movió, leído de convocatorias-service.
 * Lanzada significa `launchedAt` puesto (spec `07` §2), no "cualquier estado menos
 * `draft`": una campaña cancelada desde borrador nunca salió, y una cancelada a medio
 * envío sí cuenta.
 */
export const isLaunched = (campaign: CampaignResponse) => Boolean(campaign.launchedAt)

export const trackingKey = (eventId: string) => ['campaigns', 'tracking', eventId] as const

/**
 * Si se puede leer de convocatorias-service: el rol y el interruptor del módulo. Con Invitaciones
 * apagado el servicio puede ni estar desplegado (perfil sin `convocatorias`), y cada lectura daría 502.
 */
export function useConvocatoriasDisponible(): boolean {
  const { allowed } = useCan(ROLES.convocatoriasRead)
  const { isEnabled } = useFeatureSwitches()
  return allowed && isEnabled('convocatorias')
}

/** Si el evento tiene al menos una campaña lanzada. `undefined` mientras carga. */
export function useConvocatoriaLanzada(eventId: string): boolean | undefined {
  const { data } = useCampaigns({ eventId, pageSize: 100 }, useConvocatoriasDisponible())
  return data ? data.items.some(isLaunched) : undefined
}

/**
 * Cuántas campañas tiene cada evento y cuántas de ellas ya se lanzaron, recorriendo todas
 * las páginas. Un evento que no aparece no tiene ninguna.
 */
export function useCampanasPorEvento() {
  const canRead = useConvocatoriasDisponible()
  return useQuery({
    queryKey: ['campaigns', 'list', 'count-by-event'],
    queryFn: async () => {
      const porEvento = new Map<string, { total: number; lanzadas: number }>()
      for (let page = 1; ; page++) {
        const res = await api.listCampaigns({ page, pageSize: 100 })
        for (const c of res.items) {
          // Una campaña sin evento (mensaje directo) no cuenta para ninguno.
          if (!c.eventId) continue
          const cuenta = porEvento.get(c.eventId) ?? { total: 0, lanzadas: 0 }
          cuenta.total++
          if (isLaunched(c)) cuenta.lanzadas++
          porEvento.set(c.eventId, cuenta)
        }
        if (page * res.pageSize >= res.total || res.items.length === 0) return porEvento
      }
    },
    enabled: canRead,
  })
}

export function useEventTracking(eventId: string) {
  const canRead = useConvocatoriasDisponible()
  return useQuery({
    queryKey: trackingKey(eventId),
    queryFn: () => api.getEventTracking(eventId),
    enabled: Boolean(eventId) && canRead,
  })
}

/**
 * Abrir o cerrar la ventana de inscripción del evento. La ventana es de convocatorias-service
 * y es lo único que habilita el formulario público, así que al cambiarla se releen el resumen
 * y la cartelera.
 */
export function useToggleRegistrations(eventId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (open: boolean) =>
      open ? api.reopenEventRegistrations(eventId) : api.closeEventRegistrations(eventId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: trackingKey(eventId) })
      void queryClient.invalidateQueries({ queryKey: publicEventoKeys.list() })
    },
  })
}

/** Un resumen por evento, en paralelo. */
export function useEventTrackings(eventIds: string[]) {
  const canRead = useConvocatoriasDisponible()
  return useQueries({
    queries: eventIds.map((eventId) => ({
      queryKey: trackingKey(eventId),
      queryFn: () => api.getEventTracking(eventId),
      enabled: Boolean(eventId) && canRead,
    })),
  })
}
