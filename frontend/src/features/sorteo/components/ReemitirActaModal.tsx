import { useState, type FormEvent } from 'react'
import { Loader2 } from 'lucide-react'
import type { RaffleResponse, ReissueRaffleRequest } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import { Modal } from '@/components/ui/Modal'
import { fieldError } from '@/lib/fieldErrors'
import { useReissueRaffle } from '../hooks'

const CAMPOS: [keyof ReissueRaffleRequest, string][] = [
  ['notaryName', 'Nombre del notario'],
  ['notaryIdentityDocument', 'Documento del notario'],
  ['witnessOneName', 'Nombre del primer testigo'],
  ['witnessOneIdentityDocument', 'Documento del primer testigo'],
  ['witnessTwoName', 'Nombre del segundo testigo'],
  ['witnessTwoIdentityDocument', 'Documento del segundo testigo'],
]

/**
 * Corregir una autoridad después de completar: las seis en un cuerpo, y el servicio genera una
 * versión más del acta sin tocar las anteriores ni reenviar correos (spec raffles/03 §4.5).
 */
export function ReemitirActaModal({
  eventId,
  raffle,
  onClose,
}: {
  eventId: string
  raffle: RaffleResponse
  onClose: () => void
}) {
  const reissue = useReissueRaffle(eventId)
  const [form, setForm] = useState<ReissueRaffleRequest>(() => ({
    notaryName: raffle.notaryName ?? '',
    notaryIdentityDocument: raffle.notaryIdentityDocument ?? '',
    witnessOneName: raffle.witnessOneName ?? '',
    witnessOneIdentityDocument: raffle.witnessOneIdentityDocument ?? '',
    witnessTwoName: raffle.witnessTwoName ?? '',
    witnessTwoIdentityDocument: raffle.witnessTwoIdentityDocument ?? '',
  }))
  const siguiente = (raffle.notarialRecords[0]?.version ?? 0) + 1

  const submit = (e: FormEvent) => {
    e.preventDefault()
    reissue.mutate(form, { onSuccess: onClose })
  }

  return (
    <Modal title="Reemitir acta" onClose={onClose} width="max-w-lg">
      <form onSubmit={submit} className="space-y-4 text-xs text-slate-600 dark:text-slate-300">
        <p>
          Se genera la versión {siguiente} del acta con estos datos. Las versiones anteriores se
          conservan, y no se reenvía ningún correo a los ganadores.
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {CAMPOS.map(([campo, etiqueta]) => (
            <Field key={campo} label={etiqueta} required error={fieldError(reissue.error, campo)}>
              <TextInput
                value={form[campo]}
                onChange={(e) => setForm({ ...form, [campo]: e.target.value })}
              />
            </Field>
          ))}
        </div>
        {reissue.error != null && <ErrorNote error={reissue.error} />}
        <div className="flex justify-end gap-2.5 border-t border-slate-100 pt-4 dark:border-slate-800">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={reissue.isPending}
            className="cursor-pointer text-xs font-bold"
          >
            Volver
          </Button>
          <Button
            type="submit"
            disabled={reissue.isPending}
            className="cursor-pointer text-xs font-bold text-white bg-brand-deep hover:bg-brand-deep/90"
          >
            {reissue.isPending && <Loader2 size={14} className="mr-1.5 animate-spin" />}
            Reemitir acta
          </Button>
        </div>
      </form>
    </Modal>
  )
}
