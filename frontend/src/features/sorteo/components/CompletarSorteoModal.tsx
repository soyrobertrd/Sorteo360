import type { RaffleResponse } from '@/api/types'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { WarningNote } from '@/components/ui/Feedback'
import { apiErrorCodeLabel } from '@/lib/labels'
import { useCompleteRaffle } from '../hooks'
import { authoritiesMissing, liveDraw, prizesOf } from '../raffleView'

/**
 * Cerrar el sorteo no tiene vuelta atrás: se acaban los sorteos y las anulaciones, y cada ganador
 * recibe su correo (spec raffles/03 §4.4, §5.1). Lo que el servicio exige se avisa antes de mandar.
 */
export function CompletarSorteoModal({
  eventId,
  raffle,
  onClose,
}: {
  eventId: string
  raffle: RaffleResponse
  onClose: () => void
}) {
  const completeRaffle = useCompleteRaffle(eventId)
  const premios = prizesOf(raffle)
  const ganadores = premios.filter((p) => liveDraw(p) !== undefined).length
  const sinSortear = premios.filter((p) => p.state === 'undrawn').length
  const faltan = [
    authoritiesMissing(raffle) && apiErrorCodeLabel.RAFFLE_AUTHORITIES_REQUIRED,
    !raffle.pickupInstructions?.trim() && apiErrorCodeLabel.RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED,
  ].filter((f): f is string => Boolean(f))

  return (
    <ConfirmDialog
      title="Completar el sorteo"
      confirmLabel="Completar sorteo"
      pending={completeRaffle.isPending}
      confirmDisabled={faltan.length > 0}
      error={completeRaffle.error}
      onConfirm={() => completeRaffle.mutate(undefined, { onSuccess: onClose })}
      onCancel={onClose}
    >
      <p>Al completarlo ya no se puede sortear ni anular ningún premio.</p>
      {ganadores === 0 ? (
        <p>No queda ningún ganador vigente, así que no se envía ningún correo.</p>
      ) : (
        <p>
          Se envía un correo a {ganadores} {ganadores === 1 ? 'ganador' : 'ganadores'} con su código
          de canje y las instrucciones de retiro.
        </p>
      )}
      {sinSortear > 0 && (
        <p>{sinSortear === 1 ? 'Un premio queda' : `${sinSortear} premios quedan`} sin sortear.</p>
      )}
      <p>Se genera el acta notarial, versión 1, para imprimirla y firmarla.</p>
      {raffle.pickupInstructions?.trim() && (
        <div>
          <p className="font-bold text-slate-700 dark:text-slate-200">Instrucciones de retiro</p>
          <p className="whitespace-pre-line rounded-lg bg-slate-50 p-2 dark:bg-slate-800">
            {raffle.pickupInstructions}
          </p>
        </div>
      )}
      {faltan.length > 0 && (
        <WarningNote>
          {faltan.map((f) => (
            <p key={f}>{f}</p>
          ))}
        </WarningNote>
      )}
    </ConfirmDialog>
  )
}
