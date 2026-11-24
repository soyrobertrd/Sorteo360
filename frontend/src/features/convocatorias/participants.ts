import { useMemo } from 'react'
import { useQueries } from '@tanstack/react-query'
import { api } from '@/api'
import type { EventoListItem, ListParticipantsParams, ParticipantResponse } from '@/api/types'
import {
  ESTADOS_INSCRITOS,
  invitadosFor,
  rasgosLocalesPorCorreo,
  type Invitado,
} from './mockParticipants'
import { useCheckinsOverrides } from './store'
import { descargarBlob } from '@/lib/download'

/**
 * La lista de participantes de un evento contra convocatorias-service (spec 07 §3). El servicio
 * es la autoridad sobre quién está y si se inscribió; el tipo de participante, el puntaje, el
 * RSVP y la lista de espera no existen allí y siguen siendo la mitad local de `mockParticipants`.
 */

export const participantKeys = {
  allOfEvent: (eventId: string) => ['participants', 'event', eventId, 'all'] as const,
}

/** Todas las páginas de un evento, de 100 en 100 (el tope del servicio). */
async function listAllParticipants(eventId: string): Promise<ParticipantResponse[]> {
  const rows: ParticipantResponse[] = []
  for (let page = 1; ; page++) {
    const result = await api.listEventParticipants(eventId, { page, pageSize: 100 })
    rows.push(...result.items)
    if (result.items.length === 0 || rows.length >= result.total) return rows
  }
}

/** Los participantes de cada evento pedido, unidos. Cada evento se cachea por separado. */
export function useParticipantesDeEventos(eventIds: string[]) {
  return useQueries({
    queries: eventIds.map((eventId) => ({
      queryKey: participantKeys.allOfEvent(eventId),
      queryFn: () => listAllParticipants(eventId),
    })),
    combine: (results) => ({
      data: results.flatMap((r, index) =>
        (r.data ?? []).map((row) => ({ eventId: eventIds[index], row })),
      ),
      isLoading: results.some((r) => r.isLoading),
      error: results.find((r) => r.error != null)?.error ?? null,
    }),
  })
}

/**
 * Une la fila del servicio con la mitad local. Si el mock generó a esa persona para el evento,
 * se reutiliza su tipo, puntaje y estado (así la demo no cambia); si no, tipo y puntaje se
 * siembran por correo y el estado sale de `registered`. Un estado local que contradiga
 * `registered` cede ante el servicio.
 */
export function invitadosDesdeParticipantes(
  filas: { eventId: string; row: ParticipantResponse }[],
  eventos: EventoListItem[],
): Invitado[] {
  const eventosPorId = new Map(eventos.map((e) => [e.id, e]))
  const locales = new Map<string, Invitado>()
  for (const eventId of new Set(filas.map((f) => f.eventId))) {
    const evento = eventosPorId.get(eventId)
    if (!evento) continue
    for (const invitado of invitadosFor(evento)) {
      const key = `${eventId}:${invitado.email.trim().toLowerCase()}`
      if (!locales.has(key)) locales.set(key, invitado)
    }
  }

  return filas.map(({ eventId, row }) => {
    const id = `${eventId}:${row.email.trim().toLowerCase()}`
    const local = locales.get(id)
    const localCoincide =
      local != null && ESTADOS_INSCRITOS.includes(local.status) === row.registered
    const fechaInvitado = local?.fechaInvitado ?? '2026-07-15'
    const fechaRegistro =
      local?.fechaRegistro ??
      (row.registeredAt ? row.registeredAt.slice(0, 10) : row.registered ? '2026-07-28' : null)

    return {
      id,
      eventoId: eventId,
      eventoNombre: eventosPorId.get(eventId)?.name ?? '',
      nombre: `${row.firstName} ${row.lastName}`.trim(),
      empresa: row.company ?? '',
      email: row.email,
      ...(local ? { tipo: local.tipo, score: local.score } : rasgosLocalesPorCorreo(row.email)),
      status: localCoincide ? local.status : row.registered ? 'REGISTERED' : 'INVITED',
      fechaInvitado,
      fechaRegistro,
      detailsDiffer: row.detailsDiffer,
    }
  })
}

/** Baja el CSV que arma el servicio con los filtros que entiende. */
export async function descargarCsvParticipantes(
  eventId: string,
  params: ListParticipantsParams,
): Promise<void> {
  const { blob, fileName } = await api.exportEventParticipantsCsv(eventId, params)
  descargarBlob(blob, fileName)
}

/**
 * Los invitados de uno o varios eventos, tal como los ven todas las pantallas de Convocatorias:
 * los participantes del servicio (invitados por campaña y registrados del formulario público)
 * con el estado que dejó el check-in de esta demo. Así la página del evento, la de invitados y
 * el check-in cuentan a las mismas personas.
 */
export function useInvitadosDeEventos(eventos: EventoListItem[]) {
  const participantes = useParticipantesDeEventos(eventos.map((e) => e.id))
  const overrides = useCheckinsOverrides()
  const invitados = useMemo(
    () =>
      invitadosDesdeParticipantes(participantes.data, eventos).map((i) =>
        overrides[i.id] ? { ...i, status: overrides[i.id] } : i,
      ),
    [participantes.data, eventos, overrides],
  )
  return { invitados, isLoading: participantes.isLoading, error: participantes.error }
}
