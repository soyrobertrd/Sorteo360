import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import type { ListDoorRegistrationsParams } from '@/api/types'

export const doorKeys = {
  all: ['door'] as const,
  events: () => ['door', 'events'] as const,
  list: (eventId: string) => ['door', 'list', eventId] as const,
  page: (eventId: string, params: ListDoorRegistrationsParams) =>
    ['door', 'list', eventId, params] as const,
}

/**
 * Los eventos que el terminal puede elegir (spec convocatorias/10 §4). Se vuelven a leer cada
 * hora, para que un terminal que pasa la noche abierto pregunte otra vez cuando su evento sale de
 * la ventana (§4.1).
 */
export function useDoorEvents() {
  return useQuery({
    queryKey: doorKeys.events(),
    queryFn: () => api.listDoorEvents(),
    refetchInterval: 60 * 60_000,
  })
}

/** La lista de la puerta de un evento, con sus totales sin filtrar (spec convocatorias/09 §5). */
export function useDoorList(eventId: string, params: ListDoorRegistrationsParams) {
  return useQuery({
    queryKey: doorKeys.page(eventId, params),
    queryFn: () => api.listDoorRegistrations(eventId, params),
    enabled: Boolean(eventId),
  })
}

/**
 * Los totales del pie (*Entradas: 287/412*): ignoran los filtros, así que basta una fila. Se leen
 * tras cada check-in, por la invalidación de las escrituras, y cada 30 s por los otros terminales
 * (spec convocatorias/10 §5).
 */
export function useDoorTotals(eventId: string) {
  return useQuery({
    queryKey: doorKeys.page(eventId, { pageSize: 1 }),
    queryFn: () => api.listDoorRegistrations(eventId, { pageSize: 1 }),
    enabled: Boolean(eventId),
    refetchInterval: 30_000,
    select: (page) => page.totals,
  })
}

/**
 * Las tres escrituras de la puerta. Cada una vuelve a leer la lista del evento, y con ella los
 * totales del pie.
 */
function useDoorWrite<T, V>(eventId: string, write: (variables: V) => Promise<T>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: write,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: doorKeys.list(eventId) }),
  })
}

/** El check-in de un QrCode ya sin la URL del boleto. */
export function useCheckIn(eventId: string) {
  return useDoorWrite(eventId, (qrCode: string) => api.checkIn(eventId, qrCode))
}

/** El check-in de alguien elegido en la lista de la puerta. */
export function useCheckInByRegistration(eventId: string) {
  return useDoorWrite(eventId, (registrationId: string) =>
    api.checkInByRegistration(eventId, registrationId),
  )
}

/** Deshace un check-in elegido en la lista de la puerta. */
export function useUndoCheckIn(eventId: string) {
  return useDoorWrite(eventId, (registrationId: string) => api.undoCheckIn(eventId, registrationId))
}
