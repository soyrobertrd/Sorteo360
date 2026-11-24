import { useEffect, useState } from 'react'
import type {
  EligibleParticipantResponse,
  PrizeDrawSummary,
  PrizeResponse,
  RaffleVisualStyle,
} from '@/api/types'
import type { EstadoTablero } from './tablero'

/**
 * Lo que la consola del operador le dice a la pantalla del proyector. Viaja por
 * `BroadcastChannel`: las dos ventanas están en la misma laptop (la del proyector arrastrada a la
 * segunda pantalla), así que no hace falta red ni servidor, y funciona aunque se caiga internet.
 */
export type MensajeProyector =
  | { tipo: 'espera'; premio: PrizeResponse | null; mostrarPremio: boolean }
  | {
      tipo: 'girando'
      premio: PrizeResponse
      ganador: EligibleParticipantResponse
      bombo: EligibleParticipantResponse[]
      mostrarPremio: boolean
      modo: RaffleVisualStyle
      duracionSec: number
    }
  | {
      tipo: 'ganador'
      premio: PrizeResponse
      ganador: PrizeDrawSummary
      /** Lo que se muestra del ganador según los campos visibles del sorteo (§10). */
      visible?: { nombre: string; identificador: string | null; detalle: string | null }
    }
  | { tipo: 'presencia'; confirmada: boolean }
  /** El tablero de ganadores completo: el proyector solo lo pinta. */
  | { tipo: 'tablero'; tablero: EstadoTablero }

const canal = (eventId: string) => `sorteo360-proyector-${eventId}`

/** Abre el canal del sorteo; quien lo usa lo cierra con `close()`. Sin soporte, `null`. */
export function abrirCanalProyector(eventId: string): BroadcastChannel | null {
  return typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(canal(eventId))
}

export const rutaProyector = (eventId: string) => `/proyector/${eventId}`

/**
 * Las señales de presencia entre ventanas. El proyector manda `hola` al abrirse y al recibir un
 * `ping`, y `adios` al cerrarse; la consola manda `ping` al abrirse para saber si ya hay uno.
 */
export type SenalProyector = { tipo: 'hola' } | { tipo: 'adios' } | { tipo: 'ping' }

/**
 * Si hay una pantalla de proyector abierta para este sorteo en esta laptop. Con ella, la consola
 * pasa a ser el control remoto: no repite la pantalla del público.
 */
export function useProyectorConectado(eventId: string): boolean {
  const [conectado, setConectado] = useState(false)
  useEffect(() => {
    const c = abrirCanalProyector(eventId)
    if (!c) return
    c.onmessage = (e: MessageEvent<{ tipo?: string }>) => {
      if (e.data?.tipo === 'hola') setConectado(true)
      else if (e.data?.tipo === 'adios') setConectado(false)
    }
    c.postMessage({ tipo: 'ping' } satisfies SenalProyector)
    return () => c.close()
  }, [eventId])
  return conectado
}
