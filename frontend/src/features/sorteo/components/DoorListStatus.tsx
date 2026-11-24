import { useEffect, useState } from 'react'
import { Clock } from 'lucide-react'
import type { RaffleAttendeesResponse, RaffleState, RaffleWarning } from '@/api/types'
import { WarningNote } from '@/components/ui/Feedback'
import { formatDateTime } from '@/lib/format'
import { attendanceStatusLabel, raffleWarningLabel } from '@/lib/labels'
import { formatAge } from '../raffleView'

/**
 * Cuánto tiene la lista de la puerta y cada aviso en vigor. Son contrato, no adorno (spec
 * raffles/01 §10): quien congela el bombo tiene que saber que la lista es de hace cuarenta
 * segundos.
 */

// El aviso de la lista de DO Travel Awards venía del sistema anterior: aquí no hay tal lista.
// shortcut: se filtra en pantalla hasta que el backend deje de emitirlo (plans/sorteo360.md, A5).
const AVISOS_OCULTOS: RaffleWarning[] = ['awards_list_not_in_force']
export function DoorListStatus({
  state,
  attendees,
  warnings,
}: {
  state: RaffleState
  attendees: RaffleAttendeesResponse | undefined
  warnings: RaffleWarning[]
}) {
  const now = useNow()
  const fetchedAt = attendees?.attendanceFetchedAt ?? null
  const visibles = warnings.filter((w) => !AVISOS_OCULTOS.includes(w))

  return (
    <div className="space-y-2">
      {attendees && (
        <p className="flex flex-wrap items-center gap-1.5 text-xs text-slate-600 dark:text-slate-300">
          <Clock className="h-3.5 w-3.5 text-slate-400" />
          {state !== 'draft' ? (
            <span>Lista congelada al iniciar el sorteo: {formatDateTime(fetchedAt)}</span>
          ) : fetchedAt ? (
            <span>
              {attendanceStatusLabel[attendees.attendanceStatus]}, leída{' '}
              <strong title={formatDateTime(fetchedAt)}>{formatAge(fetchedAt, now)}</strong>.
            </span>
          ) : (
            <span>{attendanceStatusLabel[attendees.attendanceStatus]}.</span>
          )}
        </p>
      )}
      {visibles.length > 0 && (
        <WarningNote>
          <ul className="list-disc pl-5">
            {visibles.map((w) => (
              <li key={w}>{raffleWarningLabel[w]}</li>
            ))}
          </ul>
        </WarningNote>
      )}
    </div>
  )
}

/** La hora actual, al segundo de diez: lo justo para que la edad de la lista no se quede quieta. */
function useNow(): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 10_000)
    return () => window.clearInterval(id)
  }, [])
  return now
}
