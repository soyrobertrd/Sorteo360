import type { RaffleResponse } from '@/api/types'

export type AccionBitacora = 'sorteo' | 'anulacion' | 'entrega' | 'reversion'

export interface EntradaBitacora {
  fecha: string
  accion: AccionBitacora
  ronda: string
  premio: string
  participante: string
  /** Quién lo hizo; nulo si el servicio todavía no lo informa. */
  usuario: string | null
  detalle: string | null
}

export const accionLabel: Record<AccionBitacora, string> = {
  sorteo: 'Sorteo',
  anulacion: 'Anulación',
  entrega: 'Entrega',
  reversion: 'Entrega revertida',
}

/**
 * La bitácora del sorteo: cada sorteo, anulación, entrega y reversión, con fecha, ronda, premio y
 * quién lo hizo, de la más reciente a la más vieja. Se arma de los premios que ya trae el sorteo.
 */
export function bitacoraDe(raffle: RaffleResponse): EntradaBitacora[] {
  const entradas: EntradaBitacora[] = []
  for (const ronda of raffle.rounds) {
    const nombreRonda = `Ronda ${ronda.number}: ${ronda.name}`
    for (const premio of ronda.prizes) {
      for (const draw of premio.draws) {
        const base = { ronda: nombreRonda, premio: premio.name, participante: draw.winnerName }
        entradas.push({
          ...base,
          fecha: draw.drawnAt,
          accion: 'sorteo',
          usuario: draw.drawnByName ?? null,
          detalle: `Tómbola de ${draw.eligiblePoolSize}`,
        })
        if (draw.void) {
          entradas.push({
            ...base,
            fecha: draw.void.voidedAt,
            accion: 'anulacion',
            usuario: draw.void.voidedByName ?? null,
            detalle: draw.void.note,
          })
        }
        for (const h of draw.handovers) {
          entradas.push({
            ...base,
            fecha: h.redeemedAt,
            accion: 'entrega',
            usuario: h.redeemedByName ?? null,
            detalle: `Retiró ${h.receiverName}`,
          })
          if (h.reversedAt) {
            entradas.push({
              ...base,
              fecha: h.reversedAt,
              accion: 'reversion',
              usuario: null,
              detalle: h.reversalReason,
            })
          }
        }
      }
    }
  }
  return entradas.sort((a, b) => b.fecha.localeCompare(a.fecha))
}

/** La bitácora como CSV separado por `;` (el que abre bien Excel en español). */
export function bitacoraCsv(entradas: EntradaBitacora[]): string {
  const esc = (s: string | null) => `"${(s ?? '').replace(/"/g, '""')}"`
  const filas = entradas.map((e) =>
    [e.fecha, accionLabel[e.accion], e.ronda, e.premio, e.participante, e.usuario, e.detalle]
      .map(esc)
      .join(';'),
  )
  return ['Fecha;Acción;Ronda;Premio;Participante;Usuario;Detalle', ...filas].join('\r\n')
}
