import { useState } from 'react'
import type { PrizeDrawSummary, PrizeDrawVoidCode, PrizeResponse } from '@/api/types'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field, TextArea } from '@/components/ui/Field'
import { fieldError } from '@/lib/fieldErrors'
import { prizeDrawVoidCodeLabel } from '@/lib/labels'
import { useReadPrize, useVoidPrizeDraw } from '../hooks'
import { ApiError } from '@/api'
import { isLostResponse } from '../raffleView'

const NOTE_MAX_LENGTH = 500

const VOID_CODES: readonly PrizeDrawVoidCode[] = ['absent', 'declined', 'ineligible', 'other']

/**
 * Anula el ganador vivo de un premio con un motivo (spec raffles/02 §3). El premio vuelve a quedar
 * por sortear y la anulación queda en su historial; anular nunca vuelve a sortear. Una respuesta
 * perdida no se reintenta: se relee el premio, y si este sorteo quedó anulado, la anulación llegó.
 * Lo mismo con `PRIZE_NOT_DRAWN`: otra pantalla ya lo anuló, y el resultado es el que se pedía.
 */
export function VoidDrawModal({
  eventId,
  prize,
  draw,
  initialCode = 'absent',
  onVoided,
  onClose,
}: {
  eventId: string
  prize: PrizeResponse
  draw: PrizeDrawSummary
  initialCode?: PrizeDrawVoidCode
  onVoided: (prize: PrizeResponse) => void
  onClose: () => void
}) {
  const voidDraw = useVoidPrizeDraw(eventId)
  const readPrize = useReadPrize(eventId)
  const [code, setCode] = useState<PrizeDrawVoidCode>(initialCode)
  const [note, setNote] = useState('')
  const [noteMissing, setNoteMissing] = useState(false)

  const handleConfirm = () => {
    const trimmed = note.trim() || null
    if (code === 'other' && trimmed === null) {
      setNoteMissing(true)
      return
    }
    voidDraw.mutate(
      { prizeId: prize.id, body: { code, note: trimmed } },
      {
        onSuccess: onVoided,
        onError: async (error) => {
          const alreadyVoided = error instanceof ApiError && error.code === 'PRIZE_NOT_DRAWN'
          if (!alreadyVoided && !isLostResponse(error)) return
          const reread = await readPrize(prize.id)
          if (reread?.draws.some((d) => d.id === draw.id && d.void !== null)) onVoided(reread)
        },
      },
    )
  }

  const noteError = noteMissing
    ? 'Con «Otro motivo» la nota es obligatoria.'
    : fieldError(voidDraw.error, 'Note')

  return (
    <ConfirmDialog
      title="Anular ganador"
      confirmLabel="Anular ganador"
      destructive
      pending={voidDraw.isPending}
      error={voidDraw.error}
      onConfirm={handleConfirm}
      onCancel={onClose}
    >
      <p>
        <strong>{draw.winnerName}</strong> deja de ser el ganador de <strong>{prize.name}</strong>.
        El premio vuelve a quedar por sortear, y la anulación queda en su historial y en el acta.
      </p>
      <fieldset className="space-y-1.5">
        <legend className="mb-1 font-bold text-slate-700 dark:text-slate-200">Motivo</legend>
        {VOID_CODES.map((c) => (
          <label key={c} className="flex cursor-pointer items-center gap-2">
            <input
              type="radio"
              name="void-code"
              checked={code === c}
              onChange={() => {
                setCode(c)
                setNoteMissing(false)
              }}
            />
            {prizeDrawVoidCodeLabel[c]}
          </label>
        ))}
      </fieldset>
      <p className="text-slate-500 dark:text-slate-400">
        Queda fuera de todos los premios que falten en este sorteo.
      </p>
      <Field label={code === 'other' ? 'Nota' : 'Nota (opcional)'} error={noteError}>
        <TextArea
          value={note}
          maxLength={NOTE_MAX_LENGTH}
          onChange={(e) => {
            setNote(e.target.value)
            setNoteMissing(false)
          }}
          placeholder="Lo que el acta debe explicar."
        />
      </Field>
    </ConfirmDialog>
  )
}
