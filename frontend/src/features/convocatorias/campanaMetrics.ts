import type { EventoStatus } from '@/api/types'

/**
 * Una simulación determinista por `evento.id`, solo para los datos de demostración de
 * `mockParticipants.ts` (Invitados y Check-in). Ninguna pantalla de cifras la usa ya: el
 * dashboard de Convocatorias y la ficha del evento leen el resumen de convocatorias-service
 * y muestran un guion donde el servicio no tiene el dato.
 */

export interface CampanaMetrics {
  enviados: number
  abiertos: number
  registrados: number
  rechazados: number
  bajas: number
}

export type CampanaStatus = 'ACTIVA' | 'COMPLETADA' | 'PAUSADA'

/** Hash determinista simple (FNV-1a-like) para sembrar los porcentajes por evento. */
function seededRatio(eventoId: string, salt: string): number {
  let hash = 2166136261
  const str = eventoId + salt
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i)
    hash = Math.imul(hash, 16777619)
  }
  return (hash >>> 0) / 4294967295
}

export function campanaMetricsFor(evento: {
  id: string
  invitedCount?: number | null
}): CampanaMetrics {
  const enviados = evento.invitedCount ?? 150 + Math.round(seededRatio(evento.id, 'base') * 350)
  const tasaApertura = 0.55 + seededRatio(evento.id, 'apertura') * 0.3
  const abiertos = Math.round(enviados * tasaApertura)
  const tasaRegistro = 0.6 + seededRatio(evento.id, 'registro') * 0.25
  const registrados = Math.round(abiertos * tasaRegistro)
  const rechazados = Math.round(enviados * (0.005 + seededRatio(evento.id, 'rechazo') * 0.03))
  const bajas = Math.round(registrados * (0.005 + seededRatio(evento.id, 'baja') * 0.02))
  return { enviados, abiertos, registrados, rechazados, bajas }
}

/**
 * El estado de la campaña se deriva del propio evento: cancelado pausa la
 * convocatoria, cerrado la concluye, y mientras el evento sigue vigente la
 * campaña está activa. No hay un estado independiente que rastrear todavía.
 */
export function campanaStatusFor(evento: { status: EventoStatus }): CampanaStatus {
  if (evento.status === 'cancelled') return 'PAUSADA'
  if (evento.status === 'closed') return 'COMPLETADA'
  return 'ACTIVA'
}
