import type {
  DisplayDetailMode,
  DisplayIdentifierMode,
  DisplayNameMode,
  PrizeDrawSummary,
  RaffleDisplayFields,
  RaffleResponse,
} from '@/api/types'

/**
 * Qué datos del ganador se ven en la pantalla del sorteo (plans/sorteo360.md §10). Puro: la consola,
 * el proyector, el tablero y el historial piden el texto aquí. El acta y el certificado no pasan por
 * esto: siempre llevan nombre completo, boleto e ID de empleado.
 */

export const CAMPOS_VISIBLES_POR_DEFECTO: RaffleDisplayFields = {
  name: 'full_name',
  identifier: 'ticket_number',
  detail: 'location',
}

export const camposDe = (raffle: Pick<RaffleResponse, 'displayFields'> | undefined) =>
  raffle?.displayFields ?? CAMPOS_VISIBLES_POR_DEFECTO

export const OPCIONES_NOMBRE: { valor: DisplayNameMode; etiqueta: string }[] = [
  { valor: 'full_name', etiqueta: 'Nombre completo' },
  { valor: 'first_and_last', etiqueta: 'Nombre y apellido' },
  { valor: 'first_name', etiqueta: 'Solo nombre' },
]

export const OPCIONES_IDENTIFICADOR: { valor: DisplayIdentifierMode; etiqueta: string }[] = [
  { valor: 'ticket_number', etiqueta: 'Número de boleto' },
  { valor: 'employee_code', etiqueta: 'ID de empleado' },
  { valor: 'none', etiqueta: 'Ninguno' },
]

export const OPCIONES_DETALLE: { valor: DisplayDetailMode; etiqueta: string }[] = [
  { valor: 'department', etiqueta: 'Departamento' },
  { valor: 'location', etiqueta: 'Ubicación' },
  { valor: 'company', etiqueta: 'Empresa' },
  { valor: 'none', etiqueta: 'Ninguno' },
]

const limpio = (s: string | null | undefined) => s?.trim() || null

type Ganador = Pick<
  PrizeDrawSummary,
  | 'winnerName'
  | 'winnerCompany'
  | 'winnerTicketNumber'
  | 'winnerEmployeeCode'
  | 'winnerDepartment'
  | 'winnerLocation'
  | 'winnerFirstName'
  | 'winnerLastName'
>

/**
 * El nombre según el modo. "Nombre y apellido" es el primer nombre y el primer apellido; sin los
 * campos separados, cae al nombre completo (nunca a un nombre partido a ciegas).
 */
export function nombreVisible(g: Ganador, modo: DisplayNameMode): string {
  const completo = g.winnerName.trim()
  const nombre = limpio(g.winnerFirstName)
  const apellido = limpio(g.winnerLastName)
  if (modo === 'first_name') return nombre ?? completo
  if (modo === 'first_and_last') {
    if (!nombre || !apellido) return completo
    return `${nombre.split(/\s+/)[0]} ${apellido.split(/\s+/)[0]}`
  }
  return completo
}

/** El identificador, o `null` si no se muestra o el ganador no lo tiene. */
export function identificadorVisible(g: Ganador, modo: DisplayIdentifierMode): string | null {
  if (modo === 'ticket_number') return limpio(g.winnerTicketNumber)
  if (modo === 'employee_code') return limpio(g.winnerEmployeeCode)
  return null
}

/**
 * El detalle, o `null`: si el dato elegido viene vacío no se muestra nada para esa persona (nada
 * de "—" en la pantalla grande). Departamento cae a la empresa, como el agrupado del sorteo.
 */
export function detalleVisible(g: Ganador, modo: DisplayDetailMode): string | null {
  if (modo === 'department') return limpio(g.winnerDepartment) ?? limpio(g.winnerCompany)
  // Sin ubicación cargada no queda en blanco: cae al departamento.
  if (modo === 'location')
    return limpio(g.winnerLocation) ?? limpio(g.winnerDepartment) ?? limpio(g.winnerCompany)
  if (modo === 'company') return limpio(g.winnerCompany)
  return null
}

export function ganadorVisible(g: Ganador, campos: RaffleDisplayFields) {
  return {
    nombre: nombreVisible(g, campos.name),
    identificador: identificadorVisible(g, campos.identifier),
    detalle: detalleVisible(g, campos.detail),
  }
}
