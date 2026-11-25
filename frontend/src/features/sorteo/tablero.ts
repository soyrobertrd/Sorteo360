import type {
  PrizeDrawSummary,
  PrizeResponse,
  RaffleDisplayFields,
  RaffleResponse,
} from '@/api/types'
import { CAMPOS_VISIBLES_POR_DEFECTO, detalleVisible, nombreVisible } from './camposVisibles'
import { liveDraw } from './raffleView'

/**
 * El tablero de ganadores: un grupo de premios iguales ("83 premios de 10,000") se sortea por
 * lotes y cada lote se muestra como una lista de números y nombres. Lógica pura: la pantalla está
 * en `components/TableroGanadores.tsx`.
 */

export type Identificador = 'empleado' | 'boleto'

export interface AjustesTablero {
  titulo: string
  subtitulo: string
  identificador: Identificador
  /** Premios que se sortean con un solo botón (las filas del tablero). */
  porLote: number
  /** Cifras del número; se rellena con ceros a la izquierda. */
  cifras: number
  /** El color del nombre del premio. Lo elige quien arma el tablero, como los de la marca. */
  colorPremio: string
  /** Bolas y ramas de pino alrededor del tablero. */
  navidad: boolean
}

/** Como máximo 10 premios por pantalla (decisión de Robert, 2026-11-19). */
export const MAX_POR_PANTALLA = 10
export const LOTES = [1, 2, 3, 5, 10] as const

export const ajustesPorDefecto = (organizacion: string): AjustesTablero => ({
  titulo: 'Rifa Electrónica',
  subtitulo: organizacion,
  identificador: 'empleado',
  porLote: 10,
  cifras: 7,
  colorPremio: '#01AEF0',
  navidad: true,
})

const claveAjustes = (eventId: string) => `sorteo360.tablero.${eventId}`
const claveFormato = (eventId: string) => `sorteo360.formato.${eventId}`

// shortcut: ajustes y formato guardados en este navegador; pasan al servicio con A33
// (`visualStyle: 'winner_board'` y los ajustes del tablero en el sorteo).
export function leerAjustes(eventId: string, organizacion: string): AjustesTablero {
  const base = ajustesPorDefecto(organizacion)
  try {
    const raw = localStorage.getItem(claveAjustes(eventId))
    const guardados = raw ? { ...base, ...(JSON.parse(raw) as Partial<AjustesTablero>) } : base
    return { ...guardados, porLote: Math.min(Math.max(1, guardados.porLote), MAX_POR_PANTALLA) }
  } catch {
    return base
  }
}

export function guardarAjustes(eventId: string, ajustes: AjustesTablero): void {
  try {
    localStorage.setItem(claveAjustes(eventId), JSON.stringify(ajustes))
  } catch {
    // Sin almacenamiento: los ajustes duran lo que la pestaña.
  }
}

export function usaTablero(eventId: string): boolean {
  try {
    return localStorage.getItem(claveFormato(eventId)) === 'tablero'
  } catch {
    return false
  }
}

export function marcarTablero(eventId: string, tablero: boolean): void {
  try {
    if (tablero) localStorage.setItem(claveFormato(eventId), 'tablero')
    else localStorage.removeItem(claveFormato(eventId))
  } catch {
    // Sin almacenamiento se usa el formato guardado en el sorteo.
  }
}

export interface GrupoPremios {
  clave: string
  ronda: number
  nombre: string
  valor: string | null
  /** El patrocinador en texto; `null` sin patrocinador. */
  patrocinador: string | null
  premios: PrizeResponse[]
}

/** El nombre del patrocinador de un premio, o `null`. */
export const patrocinadorDe = (p: PrizeResponse) => p.sponsor?.companyName?.trim() || null

/**
 * Premios con el mismo nombre, valor y patrocinador dentro de una ronda forman un grupo, aunque no
 * lleguen seguidos: el servicio no garantiza el orden y 83 bonos iguales son un solo grupo de 83.
 * La clave sale del contenido y no de un id, así que no cambia de una lectura a otra.
 */
export function gruposDePremios(raffle: Pick<RaffleResponse, 'rounds'>): GrupoPremios[] {
  const grupos: GrupoPremios[] = []
  for (const ronda of raffle.rounds.toSorted((a, b) => a.number - b.number)) {
    const porClave = new Map<string, GrupoPremios>()
    for (const p of ronda.prizes) {
      const patrocinador = patrocinadorDe(p)
      const clave = `${ronda.id}:${p.name}\u0000${p.value ?? ''}\u0000${patrocinador ?? ''}`
      let actual = porClave.get(clave)
      if (!actual) {
        actual = {
          clave,
          ronda: ronda.number,
          nombre: p.name,
          valor: p.value,
          patrocinador,
          premios: [],
        }
        porClave.set(clave, actual)
        grupos.push(actual)
      }
      actual.premios.push(p)
    }
  }
  return grupos
}

export const pendientes = (g: GrupoPremios) => g.premios.filter((p) => p.state === 'undrawn')

/**
 * El número del tablero. Un ID solo de cifras se rellena con ceros ("0029370"); uno con letras o
 * guiones (cédula, "EMP-0124") va tal cual, porque es el ID del empleado. Sin dato, guiones.
 */
export function numeroVisible(
  draw: Pick<PrizeDrawSummary, 'winnerEmployeeCode' | 'winnerTicketNumber'>,
  identificador: Identificador,
  cifras: number,
): string {
  const crudo =
    identificador === 'empleado'
      ? (draw.winnerEmployeeCode ?? draw.winnerTicketNumber)
      : (draw.winnerTicketNumber ?? draw.winnerEmployeeCode)
  const limpio = (crudo ?? '').trim().toUpperCase()
  if (identificador === 'empleado' && /\D/.test(limpio) && /\d/.test(limpio)) return limpio
  const digitos = limpio.replace(/\D/g, '')
  return digitos ? digitos.padStart(cifras, '0') : '-'.repeat(cifras)
}

export interface FilaTablero {
  id: string
  numero: string
  nombre: string
  detalle: string | null
}

/** Todo lo que pinta el tablero; viaja tal cual al proyector. */
export interface EstadoTablero {
  titulo: string
  subtitulo: string
  ronda: number | null
  /**
   * Premio principal: el grupo tiene foto y se sortea uno a uno, con la foto grande y el ganador
   * debajo. Sin foto, lista de hasta 10 por pantalla.
   */
  principal: boolean
  /** Solo el nombre, sin el valor: en un premio en efectivo el nombre es el monto ("10,000"). */
  premioNombre: string | null
  /** "Cortesía de …" bajo el nombre del premio; nulo u omitido sin patrocinador. */
  premioPatrocinador?: string | null
  colorPremio: string
  navidad: boolean
  /** La foto del grupo: la primera que tenga alguno de sus premios. */
  premioImagen: string | null
  total: number
  quedan: number
  cifras: number
  filas: FilaTablero[]
  /** Cambia en cada lote: reinicia la animación de los números. */
  lote: number
}

/**
 * La fila de un ganador. El número sale del ajuste del tablero (siempre cifras); el nombre y el
 * detalle, de los campos visibles del sorteo (§10).
 */
export function filaDe(
  prize: PrizeResponse,
  ajustes: AjustesTablero,
  campos: RaffleDisplayFields = CAMPOS_VISIBLES_POR_DEFECTO,
): FilaTablero | null {
  const d = liveDraw(prize)
  if (!d) return null
  return {
    id: d.id,
    numero: numeroVisible(d, ajustes.identificador, ajustes.cifras),
    nombre: nombreVisible(d, campos.name).toLocaleUpperCase('es-DO'),
    detalle: detalleVisible(d, campos.detail)?.toLocaleUpperCase('es-DO') ?? null,
  }
}

/** Cada fila se detiene este tiempo después de la anterior. */
export const PASO_FILA_MS = 450
/** Lo que gira la primera fila antes de detenerse. */
export const GIRO_INICIAL_MS = 1200
/** El premio principal gira más: es el momento de la ronda. */
export const GIRO_PRINCIPAL_MS = 3500
/** Cuánto tarda en detenerse un lote de `n` filas: la consola espera esto para seguir. */
export const duracionLote = (n: number, principal = false) =>
  (principal ? GIRO_PRINCIPAL_MS : GIRO_INICIAL_MS + Math.max(0, n - 1) * PASO_FILA_MS) + 500

/**
 * Las filas de un lote en orden al azar (Fisher-Yates con `crypto`). Cada ganador ya sale al azar
 * del servicio, pero en el orden de los premios; RR. HH. pidió que un lote nunca se vea ordenado
 * de forma ascendente ni descendente, así que la pantalla además baraja su orden.
 */
export function barajar<T>(filas: readonly T[]): T[] {
  const out = [...filas]
  for (let i = out.length - 1; i > 0; i--) {
    const j = crypto.getRandomValues(new Uint32Array(1))[0] % (i + 1)
    ;[out[i], out[j]] = [out[j], out[i]]
  }
  return out
}
