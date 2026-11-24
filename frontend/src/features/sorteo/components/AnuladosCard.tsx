import { UserX, RotateCcw } from 'lucide-react'
import { toast } from 'sonner'
import type { RaffleResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { formatDateTime } from '@/lib/format'
import { prizeDrawVoidCodeLabel } from '@/lib/labels'
import { useReinstateRaffleAttendee } from '../hooks'
import { prizesOf } from '../raffleView'

/**
 * Quienes tuvieron un premio anulado (por no presentarse a tiempo u otro motivo) y por eso no
 * entran al bombo de los premios siguientes. Un administrador puede reactivarlos: vuelven a
 * participar desde el próximo premio. Solo con el sorteo en curso.
 */
export function AnuladosCard({ raffle }: { raffle: RaffleResponse }) {
  const canWrite = useCan(ROLES.rafflesWrite)
  const reinstate = useReinstateRaffleAttendee(raffle.eventId)
  const reactivados = new Set(raffle.reinstatedAttendeeIds ?? [])
  const premios = prizesOf(raffle)
  // Quien hoy tiene un premio vivo ya no está "fuera por anulación": ganó otro.
  const ganadoresVivos = new Set(
    premios.flatMap((p) => p.draws.filter((d) => d.void === null).map((d) => d.winnerAttendeeId)),
  )

  // La última anulación de cada persona, para mostrar su motivo.
  const porPersona = new Map<
    string,
    { nombre: string; premio: string; motivo: string; fecha: string }
  >()
  for (const premio of premios) {
    for (const draw of premio.draws) {
      if (
        !draw.void ||
        reactivados.has(draw.winnerAttendeeId) ||
        ganadoresVivos.has(draw.winnerAttendeeId)
      ) {
        continue
      }
      const previa = porPersona.get(draw.winnerAttendeeId)
      if (previa && previa.fecha > draw.void.voidedAt) continue
      porPersona.set(draw.winnerAttendeeId, {
        nombre: draw.winnerName,
        premio: premio.name,
        motivo: prizeDrawVoidCodeLabel[draw.void.code],
        fecha: draw.void.voidedAt,
      })
    }
  }
  const anulados = [...porPersona.entries()]
  if (anulados.length === 0) return null

  const enCurso = raffle.state === 'in_progress'
  const titulo = !canWrite.allowed
    ? canWrite.title
    : enCurso
      ? undefined
      : 'Solo se puede reactivar con el sorteo en curso.'

  return (
    <section className="rounded-2xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-900 dark:bg-amber-950">
      <div className="flex items-start gap-3">
        <UserX className="mt-0.5 h-5 w-5 shrink-0 text-amber-700 dark:text-amber-400" />
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-black text-foreground">
            Fuera de los próximos premios ({anulados.length})
          </h3>
          <p className="text-xs text-muted-foreground">
            Su premio se anuló y no entran a la tómbola. Al reactivarlos vuelven a participar desde
            el próximo premio.
          </p>
          <ul className="mt-3 divide-y divide-amber-200 dark:divide-amber-900">
            {anulados.map(([attendeeId, a]) => (
              <li
                key={attendeeId}
                className="flex flex-wrap items-center justify-between gap-2 py-2"
              >
                <div className="min-w-0 text-xs">
                  <p className="font-bold text-foreground">{a.nombre}</p>
                  <p className="text-muted-foreground">
                    {a.motivo} · {a.premio} · {formatDateTime(a.fecha)}
                  </p>
                </div>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={!canWrite.allowed || !enCurso || reinstate.isPending}
                  title={titulo}
                  onClick={() =>
                    reinstate.mutate(attendeeId, {
                      onSuccess: () => toast.success(`${a.nombre} vuelve a participar.`),
                    })
                  }
                  className="cursor-pointer text-xs font-bold disabled:cursor-not-allowed"
                >
                  <RotateCcw className="mr-1 h-3.5 w-3.5" /> Reactivar
                </Button>
              </li>
            ))}
          </ul>
          {reinstate.error != null && <ErrorNote error={reinstate.error} />}
        </div>
      </div>
    </section>
  )
}
