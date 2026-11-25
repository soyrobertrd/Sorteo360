import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import type { RaffleParticipantItem, RaffleResponse } from '@/api/types'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorNote } from '@/components/ui/Feedback'
import { formatDate } from '@/lib/format'
import { useAllRaffles, useImportRaffleParticipants } from '../hooks'
import { noGanadores } from '../noGanadores'
import { prizesOf } from '../raffleView'

/**
 * Carga como padrón de otro sorteo, en borrador, a los participantes de este que no ganaron. El
 * padrón del sorteo de destino se reemplaza: esos pasan a ser sus participantes.
 */
export function PasarNoGanadoresModal({
  raffle,
  participantes,
  onClose,
}: {
  raffle: RaffleResponse
  participantes: RaffleParticipantItem[]
  onClose: () => void
}) {
  const { data: sorteos, isLoading: cargando } = useAllRaffles()
  const [destino, setDestino] = useState('')
  const [confirmar, setConfirmar] = useState(false)
  const importar = useImportRaffleParticipants(destino)

  // Solo un borrador recibe padrón: uno iniciado ya congeló el suyo.
  const destinos = useMemo(
    () => (sorteos ?? []).filter((s) => s.eventId !== raffle.eventId && s.state === 'draft'),
    [sorteos, raffle.eventId],
  )
  const filas = useMemo(
    () =>
      noGanadores(
        participantes,
        prizesOf(raffle).flatMap((p) => p.draws),
      ),
    [participantes, raffle],
  )
  const nombreDestino = destinos.find((s) => s.eventId === destino)?.eventName ?? ''
  const ganadores = participantes.length - filas.length

  const pasar = () =>
    importar.mutate(
      { participants: filas, replaceExisting: true, markCheckedIn: true, source: 'manual' },
      {
        onSuccess: (res) => {
          toast.success(
            `Se cargaron ${res.total.toLocaleString('es-DO')} participantes en «${nombreDestino}».`,
          )
          onClose()
        },
        onError: () => setConfirmar(false),
      },
    )

  return (
    <Modal title="Pasar los que no ganaron a otro sorteo" onClose={onClose}>
      <div className="space-y-4 text-xs">
        <p className="text-slate-600 dark:text-slate-300">
          De {participantes.length.toLocaleString('es-DO')} participantes,{' '}
          <strong>{filas.length.toLocaleString('es-DO')}</strong> no tienen un premio vivo
          {ganadores > 0 && ` (quedan fuera ${ganadores.toLocaleString('es-DO')} ganadores)`}. Un
          ganador anulado cuenta como no ganador.
        </p>

        <label className="block space-y-1 font-semibold text-slate-700 dark:text-slate-200">
          <span>Sorteo de destino</span>
          <select
            aria-label="Sorteo de destino"
            value={destino}
            onChange={(e) => setDestino(e.target.value)}
            className="w-full rounded-lg border border-slate-200 bg-white px-2 py-1.5 font-normal dark:border-slate-700 dark:bg-slate-900"
          >
            <option value="">
              {cargando ? 'Cargando sorteos…' : 'Elija un sorteo en borrador…'}
            </option>
            {destinos.map((s) => (
              <option key={s.eventId} value={s.eventId}>
                {s.eventName} · {formatDate(s.eventStartDate)}
              </option>
            ))}
          </select>
          <span className="block font-normal text-slate-500">
            Solo aparecen sorteos en borrador. Su padrón actual se reemplaza por esta lista.
          </span>
        </label>

        {!cargando && destinos.length === 0 && (
          <p className="text-amber-700 dark:text-amber-300">
            No hay otro sorteo en borrador. Cree uno primero y vuelva aquí.
          </p>
        )}

        {importar.error != null && <ErrorNote error={importar.error} />}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            type="button"
            disabled={!destino || filas.length === 0 || importar.isPending}
            onClick={() => setConfirmar(true)}
          >
            Pasar {filas.length.toLocaleString('es-DO')} participantes
          </Button>
        </div>
      </div>

      {confirmar && (
        <ConfirmDialog
          title="Reemplazar el padrón"
          confirmLabel="Reemplazar padrón"
          destructive
          pending={importar.isPending}
          error={importar.error}
          onConfirm={pasar}
          onCancel={() => setConfirmar(false)}
        >
          <p>
            El padrón de <strong>{nombreDestino}</strong> se reemplaza por{' '}
            {filas.length.toLocaleString('es-DO')} participantes de este sorteo que no ganaron.
          </p>
        </ConfirmDialog>
      )}
    </Modal>
  )
}
