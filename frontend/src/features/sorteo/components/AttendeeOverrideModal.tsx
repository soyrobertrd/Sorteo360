import { useState } from 'react'
import type { AttendeeOverrideDecision, RaffleAttendeeResponse } from '@/api/types'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field, TextArea } from '@/components/ui/Field'
import { fieldError } from '@/lib/fieldErrors'
import { raffleExclusionReasonLabel } from '@/lib/labels'

const NOTE_MAX_LENGTH = 1000

/**
 * Pasa a alguien de una lista a la otra escribiendo un ajuste a mano, con una nota opcional para
 * el próximo operador. La nota se guarda con el ajuste y la copia el acta al congelar.
 */
export function AttendeeOverrideModal({
  attendee,
  decision,
  pending,
  error,
  onConfirm,
  onCancel,
}: {
  attendee: RaffleAttendeeResponse
  decision: AttendeeOverrideDecision
  pending: boolean
  error: unknown
  onConfirm: (note: string | null) => void
  onCancel: () => void
}) {
  const [note, setNote] = useState(attendee.manualNote ?? '')
  const name = `${attendee.firstName} ${attendee.lastName}`.trim()
  const including = decision === 'included'

  return (
    <ConfirmDialog
      title={including ? 'Incluir en el sorteo' : 'Excluir del sorteo'}
      confirmLabel={including ? 'Incluir' : 'Excluir'}
      destructive={!including}
      pending={pending}
      error={error}
      onConfirm={() => onConfirm(note.trim() || null)}
      onCancel={onCancel}
    >
      <p>
        {including ? (
          <>
            <strong>{name}</strong> pasa a los calificados.
            {attendee.exclusionReason && (
              <> Hoy no califica: {raffleExclusionReasonLabel[attendee.exclusionReason]}</>
            )}
          </>
        ) : (
          <>
            <strong>{name}</strong> sale de la tómbola y pasa a los no calificados.
          </>
        )}
      </p>
      <Field label="Nota (opcional)" error={fieldError(error, 'Note')}>
        <TextArea
          value={note}
          maxLength={NOTE_MAX_LENGTH}
          onChange={(e) => setNote(e.target.value)}
          placeholder="Por qué, para quien revise el sorteo después."
        />
      </Field>
    </ConfirmDialog>
  )
}
