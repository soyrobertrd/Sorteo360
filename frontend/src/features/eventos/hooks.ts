import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import {
  type CategoriaProveedor,
  type CreateEventoRequest,
  type ListEventosParams,
  type UpdateEventoRequest,
} from '@/api/types'
import { EventoSinCiudadError, type CambioDeCiudad } from './ciudadDelEvento'

export const eventoKeys = {
  list: (params: ListEventosParams) => ['eventos', 'list', params] as const,
  detail: (id: string) => ['eventos', 'detail', id] as const,
  cotizacion: (eventoId: string, categoria: CategoriaProveedor) =>
    ['eventos', eventoId, 'cotizacion', categoria] as const,
  /** Las filas del listado de cotizaciones de un evento, que es lo que lee la campanita. */
  cotizacionesDelEvento: (eventoId: string) => ['eventos', eventoId, 'cotizaciones'] as const,
  /** Cola transversal a los eventos, así que su clave no cuelga de ninguno. */
  cotizacionesPendientes: () => ['cotizaciones', 'pendientes'] as const,
}

export function useEventosList(params: ListEventosParams, enabled = true) {
  return useQuery({
    queryKey: eventoKeys.list(params),
    queryFn: () => api.listEventos(params),
    enabled,
  })
}

/** Sin id no pregunta nada: una campaña sin evento no tiene evento que leer. */
export function useEvento(id: string) {
  return useQuery({
    queryKey: eventoKeys.detail(id),
    queryFn: () => api.getEvento(id),
    enabled: Boolean(id),
  })
}

function useInvalidateEvento(eventoId?: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['eventos'] })
    if (eventoId) void queryClient.invalidateQueries({ queryKey: eventoKeys.detail(eventoId) })
  }
}

export interface CreateEventoVars {
  body: CreateEventoRequest
}

export interface UpdateEventoVars {
  body: UpdateEventoRequest
  /** El PATCH de su ciudad (`cambioDeCiudad`). Falta cuando la ciudad no cambió, y entonces no se manda. */
  ciudad?: CambioDeCiudad
}

export function useCreateEvento() {
  const invalidate = useInvalidateEvento()
  return useMutation({
    mutationFn: ({ body }: CreateEventoVars) => api.createEvento(body),
    onSuccess: invalidate,
  })
}

export function useUpdateEvento(eventoId: string) {
  const invalidate = useInvalidateEvento(eventoId)
  return useMutation({
    mutationFn: async ({ body, ciudad }: UpdateEventoVars) => {
      // El evento se guarda primero porque es el que más valida: si lo rechaza, nada queda a medias.
      const guardado = await api.updateEvento(eventoId, body)
      if (!ciudad) return guardado
      try {
        const actualizada = await api.updateCiudad(eventoId, ciudad.ciudadId, ciudad.body)
        return {
          ...guardado,
          cities: guardado.cities.map((c) => (c.id === actualizada.id ? actualizada : c)),
        }
      } catch (error) {
        throw new EventoSinCiudadError(error)
      }
    },
    // Una edición a medias también guardó el evento: la ficha tiene que traerlo.
    onSettled: invalidate,
  })
}

export function useEventoLifecycle(eventoId: string) {
  const invalidate = useInvalidateEvento(eventoId)
  const iniciar = useMutation({
    mutationFn: () => api.iniciarEvento(eventoId),
    onSuccess: invalidate,
  })
  const cerrar = useMutation({
    mutationFn: () => api.cerrarEvento(eventoId),
    onSuccess: invalidate,
  })
  const cancelar = useMutation({
    mutationFn: (razon: string) => api.cancelarEvento(eventoId, razon),
    onSuccess: invalidate,
  })
  return { iniciar, cerrar, cancelar }
}
