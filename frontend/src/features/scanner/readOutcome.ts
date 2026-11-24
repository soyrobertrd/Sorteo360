import {
  AlertCircle,
  Award,
  CheckCircle2,
  Clock,
  HelpCircle,
  Loader2,
  Search,
  type LucideIcon,
} from 'lucide-react'
import { ApiError } from '@/api'
import type { CheckInResponse } from '@/api/types'
import { errorMessage } from '@/lib/errorMessage'
import { formatTime, fullName } from '@/lib/format'
import { scannerRefusalTitleFor } from '@/lib/labels'

/** Qué pasó con una lectura del terminal, de la cámara, del teclado o de la lista de la puerta. */
export type ReadOutcome =
  | { kind: 'checking' }
  | { kind: 'checkedIn'; checkIn: CheckInResponse }
  | { kind: 'ticketRefused'; error: unknown }
  /** Con `raffles:draw`: se consulta el código. */
  | { kind: 'prize'; code: string }
  /** Sin `raffles:draw`: no se llama a nada (spec convocatorias/10 §3.1). */
  | { kind: 'prizeRefused' }
  | { kind: 'reference'; reference: string }
  | { kind: 'unrecognised' }

/** Una lectura de este terminal, en memoria: son del operador, no del evento (§5). */
export interface Read {
  id: number
  at: number
  outcome: ReadOutcome
}

export type ReadTone = 'ok' | 'warning' | 'error' | 'info'

/** Cómo se muestra una lectura: la tarjeta de la última y la línea de las anteriores. */
export interface ReadView {
  tone: ReadTone
  icon: LucideIcon
  /** El ícono gira: la lectura sigue en curso. */
  busy?: boolean
  title: string
  /** Quién, en negrita y en la línea del historial. */
  subject?: string
  lines: string[]
  /** La tarjeta ofrece abrir la lista de la puerta, con esta búsqueda. */
  doorListSearch?: string
}

const codeOf = (error: unknown) => (error instanceof ApiError ? error.code : undefined)

/**
 * La única traducción de una lectura a pantalla. Un código de premio con `raffles:draw` se
 * consulta, así que su tarjeta la arma `PrizeCard`; aquí queda solo su línea del historial.
 */
export function describeRead(outcome: ReadOutcome): ReadView {
  switch (outcome.kind) {
    case 'checking':
      return { tone: 'info', icon: Loader2, busy: true, title: 'Validando entrada…', lines: [] }
    case 'checkedIn': {
      const c = outcome.checkIn
      const datos = `${c.company ?? 'Sin empresa'} • ${c.reference}`
      return c.alreadyCheckedIn
        ? {
            tone: 'warning',
            icon: Clock,
            title: 'Ya había ingresado',
            subject: fullName(c),
            lines: [`Entró a las ${formatTime(c.checkedInAt)}`, datos],
          }
        : {
            tone: 'ok',
            icon: CheckCircle2,
            title: 'Check-in registrado',
            subject: fullName(c),
            lines: [datos],
          }
    }
    case 'ticketRefused': {
      const code = codeOf(outcome.error)
      return {
        tone: 'error',
        icon: AlertCircle,
        title: scannerRefusalTitleFor(code) ?? 'No se pudo validar la entrada',
        lines: [errorMessage(outcome.error)],
        // El mensaje de una entrada inexistente manda a buscar a la persona en la lista
        doorListSearch: code === 'CHECK_IN_CODE_NOT_FOUND' ? '' : undefined,
      }
    }
    case 'prize':
      return {
        tone: 'info',
        icon: Award,
        title: 'Código de premio',
        subject: outcome.code,
        lines: [],
      }
    case 'prizeRefused':
      return {
        tone: 'warning',
        icon: Award,
        title: 'Código de premio',
        lines: ['Código de premio: diríjase al mostrador de premios.'],
      }
    case 'reference':
      return {
        tone: 'info',
        icon: Search,
        title: 'Referencia',
        subject: outcome.reference,
        lines: [
          'Una referencia no da entrada. Busque a la persona en la lista, mire su documento y haga el check-in desde allí.',
        ],
        doorListSearch: outcome.reference,
      }
    case 'unrecognised':
      return {
        tone: 'error',
        icon: HelpCircle,
        title: 'Código no reconocido',
        lines: ['No es una entrada ni un código de premio.'],
        doorListSearch: '',
      }
  }
}
