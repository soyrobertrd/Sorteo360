import { useQuery } from '@tanstack/react-query'
import { api } from '@/api'

/**
 * La cartelera pública lee de convocatorias-service, no de events-service: un visitante anónimo
 * no puede leer el evento completo, y el listado ya viene filtrado a lo público y activo.
 */
export const publicEventoKeys = {
  list: () => ['publico', 'eventos'] as const,
  detail: (id: string) => ['publico', 'eventos', id] as const,
}

export function usePublicEventos() {
  return useQuery({
    queryKey: publicEventoKeys.list(),
    queryFn: () => api.listPublicEventos(),
  })
}

export function usePublicEvento(id: string) {
  return useQuery({
    queryKey: publicEventoKeys.detail(id),
    queryFn: () => api.getPublicEvento(id),
    enabled: Boolean(id),
  })
}
