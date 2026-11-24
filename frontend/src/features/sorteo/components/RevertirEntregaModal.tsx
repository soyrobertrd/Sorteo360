import { useState } from 'react'
import type { PrizeDrawSummary, PrizeHandoverSummary, PrizeResponse } from '@/api/types'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field, TextArea } from '@/components/ui/Field'
import { formatDateTime } from '@/lib/format'
import { fieldError } from '@/lib/fieldErrors'
import { useReversePrizeHandover } from '../hooks'

const REASON_MAX_LENGTH = 500

/**
 * Deshacer una entrega mal marcada: la entrega y su firma quedan, con quién, cuándo y por qué se
 * revirtió, y el premio vuelve a estar pendiente de entrega (spec raffles/03 §3.3). No hay forma de
 * desmarcarla sin motivo.
 */
export function RevertirEntregaModal({
  eventId,
  prize,
  draw,
  handover,
  onClose,
}: {
  eventId: string
  prize: PrizeResponse
  draw: PrizeDrawSummary
  handover: PrizeHandoverSummary
  onClose: () => void
}) {
  const reverse = useReversePrizeHandover(eventId)
  const [reason, setReason] = useState('')
  const [reasonMissing, setReasonMissing] = useState(false)

  const handleConfirm = () => {
    const trimmed = reason.trim()
    if (!trimmed) {
      setReasonMissing(true)
      return
    }
    reverse.mutate({ prizeId: prize.id, body: { reason: trimmed } }, { onSuccess: onClose })
  }

  const reasonError = reasonMissing
    ? 'El motivo de la reversión es obligatorio.'
    : fieldError(reverse.error, 'Reason')

  return (
    <ConfirmDialog
      title="Revertir entrega"
      confirmLabel="Revertir entrega"
      destructive
      pending={reverse.isPending}
      error={reverse.error}
      onConfirm={handleConfirm}
      onCancel={onClose}
    >
      <p>
        <strong>{prize.name}</strong> se marcó entregado a <strong>{handover.receiverName}</strong>{' '}
        el {formatDateTime(handover.redeemedAt)}. Al revertirlo vuelve a quedar pendiente de entrega
        a <strong>{draw.winnerName}</strong>.
      </p>
      <p className="text-slate-500 dark:text-slate-400">
        La entrega no se borra: queda en el historial del premio, con su firma y este motivo.
      </p>
      <Field label="Motivo" error={reasonError}>
        <TextArea
          value={reason}
          maxLength={REASON_MAX_LENGTH}
          onChange={(e) => {
            setReason(e.target.value)
            setReasonMissing(false)
          }}
          placeholder="Ej: Se marcó el premio equivocado."
        />
      </Field>
    </ConfirmDialog>
  )
}
