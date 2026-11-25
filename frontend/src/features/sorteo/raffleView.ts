import { api, ApiError } from '@/api'
import { NETWORK_ERROR_STATUS } from '@/api/http'
import type {
  NotarialRecordResponse,
  PoolExhaustedCounts,
  PrizeDrawSummary,
  PrizeHandoverSummary,
  PrizeResponse,
  RaffleExclusionReason,
  RaffleResponse,
} from '@/api/types'

/** Los premios de todas las rondas, en el orden en que corren. */
export function prizesOf(raffle: RaffleResponse): PrizeResponse[] {
  return raffle.rounds.flatMap((r) => r.prizes)
}

/** El ganador vigente: el sorteo sin anular. */
export function liveDraw(prize: PrizeResponse): PrizeDrawSummary | undefined {
  return prize.draws.find((d) => d.void === null)
}

/** La entrega vigente del sorteo: la que no se revirtió. */
export function liveHandover(draw: PrizeDrawSummary): PrizeHandoverSummary | undefined {
  return draw.handovers.find((h) => h.reversedAt === null)
}

/** Los motivos que ninguna decisión manual vence: la fila no ofrece moverla (spec raffles/01 §10). */
export const UNOVERRIDABLE_EXCLUSIONS: readonly RaffleExclusionReason[] = [
  'walk_in',
  'contact_unresolved',
  'suspended',
  'do_not_invite',
]

/** Falta alguna de las seis autoridades que `complete` exige (spec raffles/00 §6). */
export function authoritiesMissing(raffle: RaffleResponse): boolean {
  return [
    raffle.notaryName,
    raffle.notaryIdentityDocument,
    raffle.witnessOneName,
    raffle.witnessOneIdentityDocument,
    raffle.witnessTwoName,
    raffle.witnessTwoIdentityDocument,
  ].some((a) => !a?.trim())
}

/** Reabrir solo cabe sin ningún sorteo de premio, vivo o anulado (spec raffles/02 §6). */
export function hasAnyDraw(raffle: RaffleResponse): boolean {
  return prizesOf(raffle).some((p) => p.draws.length > 0)
}

/** Cuánto hace de `iso`, corto: `hace 40 s`, `hace 3 min`, `hace 2 h`. */
export function formatAge(iso: string, now: number): string {
  const seconds = Math.max(0, Math.round((now - Date.parse(iso)) / 1000))
  if (seconds < 60) return `hace ${seconds} s`
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `hace ${minutes} min`
  return `hace ${Math.floor(minutes / 60)} h`
}

/**
 * Una respuesta perdida: la red falló o contestó el proxy, y el servicio pudo haber escrito igual.
 * Se relee el sorteo para saber qué pasó, nunca se reintenta (spec raffles/02 §7.1).
 */
export function isLostResponse(error: unknown): boolean {
  return (
    !(error instanceof ApiError) || error.status === NETWORK_ERROR_STATUS || error.status >= 500
  )
}

/** El ganador que trae `PRIZE_ALREADY_DRAWN`: el sorteo vivo del premio. */
export function alreadyDrawnWinner(error: unknown): PrizeDrawSummary | undefined {
  if (!(error instanceof ApiError) || error.code !== 'PRIZE_ALREADY_DRAWN') return undefined
  const draw = error.extensions.draw as Partial<PrizeDrawSummary> | undefined
  return typeof draw?.winnerName === 'string' ? (draw as PrizeDrawSummary) : undefined
}

/** Las cuentas que trae `RAFFLE_POOL_EXHAUSTED`: quién quedó fuera y por qué regla. */
export function poolExhaustedCounts(error: unknown): PoolExhaustedCounts | undefined {
  if (!(error instanceof ApiError) || error.code !== 'RAFFLE_POOL_EXHAUSTED') return undefined
  const e = error.extensions
  const keys = ['participants', 'excludedByVoid', 'excludedByPerson', 'excludedByCompany'] as const
  if (!keys.every((k) => typeof e[k] === 'number')) return undefined
  return e as unknown as PoolExhaustedCounts
}

/**
 * Descarga una versión del acta por su archivo; no hay ruta de descarga propia del sorteo (spec
 * raffles/03 §4.6). Un enlace y no una ventana: después de esperar los bytes, el navegador bloquea
 * las ventanas pero no las descargas.
 */
export async function downloadNotarialRecord(record: NotarialRecordResponse): Promise<void> {
  const url = await api.getFileContentUrl(record.storedFileId)
  const link = document.createElement('a')
  link.href = url
  link.download = `Acta_sorteo_v${record.version}.pdf`
  link.target = '_blank'
  link.rel = 'noopener'
  link.click()
  // La descarga ya tomó los bytes; un minuto de margen para navegadores lentos.
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}

/**
 * Sorteo sin puerta: el padrón llega por archivo, API o webhook y todos están presentes, así que
 * no hay escáner ni check-in que mostrar. Es el sorteo que se crea solo, sin evento (A35).
 */
export const sinPuerta = (raffle: Pick<RaffleResponse, 'requiresCheckIn'>): boolean =>
  raffle.requiresCheckIn === false

/** Premios iguales (mismo nombre y valor) de una ronda: una línea con su cantidad. */
export interface GrupoRonda {
  clave: string
  premios: PrizeResponse[]
}

/** Agrupa por nombre y valor, en el orden en que aparece cada premio por primera vez. */
export function agruparPremios(premios: PrizeResponse[]): GrupoRonda[] {
  const grupos = new Map<string, GrupoRonda>()
  for (const p of premios) {
    // El patrocinador también separa: dos bonos iguales de patrocinadores distintos no se juntan.
    const clave = `${p.name}\u0000${p.value ?? ''}\u0000${p.sponsor?.companyName ?? ''}`
    const g = grupos.get(clave)
    if (g) g.premios.push(p)
    else grupos.set(clave, { clave, premios: [p] })
  }
  return [...grupos.values()]
}

/** El correo para mostrar, o «Sin correo» si la persona entró solo con su ID. */
export const correoOSinCorreo = (email: string | null | undefined): string =>
  email?.trim() || 'Sin correo'
