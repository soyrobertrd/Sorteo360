import { useQueries } from '@tanstack/react-query'
import { api } from '@/api'
import { useAuth } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import type { ListNotificationsParams } from '@/api/types'

/**
 * Lo que pasó con los correos de una campaña después de salir: entregados, rebotados,
 * suprimidos, fallidos y bajas. No lo sabe convocatorias-service; lo guarda el historial de
 * notification-service, que marca cada envío con la campaña de la que trata. Cada cifra es el
 * `total` de una lectura de una sola fila con su filtro.
 *
 * Por evento no sirve para sumar campañas: los correos de confirmación de inscripción llevan
 * la misma marca de evento y entrarían en la cuenta.
 *
 * Exige `notifications:read`: sin ese permiso el servicio contesta 403, así que no se pregunta
 * y `disponible` queda en falso para que la pantalla lo diga en vez de mostrar ceros.
 * Las aperturas no existen en ningún servicio.
 */
export const ENTREGA_FILTROS = {
  entregados: { deliveryStatus: 'delivered' },
  rebotados: { deliveryStatus: 'bounced' },
  suprimidos: { deliveryStatus: 'dropped' },
  fallidos: { deliveryStatus: 'failed' },
  bajas: { recipientAction: 'unsubscribed' },
} as const satisfies Record<string, ListNotificationsParams>

export type EntregaClave = keyof typeof ENTREGA_FILTROS

/** Los envíos de una campaña en el historial: el mismo filtro para las cifras y para la lista. */
export const entregaParams = (campaignId: string): ListNotificationsParams => ({
  sourceService: 'convocatorias',
  entityType: 'campaign',
  entityId: campaignId,
})

export function useEntregas(campaignId: string | undefined) {
  const { hasRole } = useAuth()
  const disponible = hasRole(ROLES.notificationsRead)
  const claves = Object.keys(ENTREGA_FILTROS) as EntregaClave[]

  const queries = useQueries({
    queries: claves.map((clave) => {
      const params = { ...entregaParams(campaignId ?? ''), ...ENTREGA_FILTROS[clave], pageSize: 1 }
      return {
        queryKey: ['notifications', 'list', params] as const,
        queryFn: () => api.listNotifications(params),
        enabled: disponible && Boolean(campaignId),
      }
    }),
  })

  const counts = Object.fromEntries(
    claves.map((clave, i) => [clave, queries[i].data?.total]),
  ) as Record<EntregaClave, number | undefined>

  return { disponible, counts, error: queries.find((q) => q.error != null)?.error ?? null }
}
