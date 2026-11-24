import { useEffect, useRef, useState } from 'react'
import SignaturePad from 'signature_pad'
import type { PrizeDrawSummary, PrizeResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field, TextInput } from '@/components/ui/Field'
import { Modal } from '@/components/ui/Modal'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { formatDateTime } from '@/lib/format'
import { fieldError } from '@/lib/fieldErrors'
import { useHandOverPrize } from '../hooks'
import { liveDraw, liveHandover } from '../raffleView'

/** El lienzo en PNG, como lo exige el servicio (spec raffles/03 §3.2). */
function firmaPng(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Sin firma'))), 'image/png'),
  )
}

/**
 * La entrega de un premio en el mostrador: quién lo retira, su documento, si es el ganador y su
 * firma en el lienzo (spec raffles/03 §3). Confirmar espera a que el lienzo tenga trazo; que la firma
 * no sea un garabato lo controla quien atiende, no el servicio. Retirar por otro se permite y queda
 * dicho. Si el premio no está para entregar, lo dice en lugar del formulario: el enlace del escáner
 * puede llegar tarde.
 */
export function EntregaPremioModal({
  eventId,
  prize,
  onClose,
}: {
  eventId: string
  prize: PrizeResponse | undefined
  onClose: () => void
}) {
  // Aquí y no en el formulario: al entregar, el premio en caché pasa a entregado y el formulario se
  // desmonta antes de que `mutate` avise del éxito
  const handOver = useHandOverPrize(eventId)
  const draw = prize && liveDraw(prize)
  const entregada = draw && liveHandover(draw)

  if (!prize || !draw || (entregada && !handOver.isSuccess)) {
    return (
      <Modal title="Registrar entrega" onClose={onClose} width="max-w-md">
        <div className="space-y-4 text-xs text-slate-600 dark:text-slate-300">
          <p>
            {!prize
              ? 'El premio no existe en este sorteo.'
              : entregada
                ? `${prize.name} ya se entregó a ${entregada.receiverName} el ${formatDateTime(entregada.redeemedAt)}.`
                : `${prize.name} no tiene un ganador vigente por entregar.`}
          </p>
          <div className="flex justify-end border-t border-slate-100 pt-4 dark:border-slate-800">
            <Button type="button" variant="outline" onClick={onClose} className="text-xs font-bold">
              Cerrar
            </Button>
          </div>
        </div>
      </Modal>
    )
  }

  return <FormularioEntrega handOver={handOver} prize={prize} draw={draw} onClose={onClose} />
}

function FormularioEntrega({
  handOver,
  prize,
  draw,
  onClose,
}: {
  handOver: ReturnType<typeof useHandOverPrize>
  prize: PrizeResponse
  draw: PrizeDrawSummary
  onClose: () => void
}) {
  const canDraw = useCan(ROLES.rafflesDraw)
  const [esGanador, setEsGanador] = useState<boolean | null>(null)
  const [nombre, setNombre] = useState('')
  const [documento, setDocumento] = useState('')
  const [conTrazo, setConTrazo] = useState(false)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const padRef = useRef<SignaturePad | null>(null)

  useEffect(() => {
    const canvas = canvasRef.current!
    // El lienzo se dibuja a la densidad de la pantalla, o el trazo sale borroso
    const ratio = Math.max(window.devicePixelRatio || 1, 1)
    canvas.width = canvas.offsetWidth * ratio
    canvas.height = canvas.offsetHeight * ratio
    canvas.getContext('2d')!.scale(ratio, ratio)
    // Fondo blanco: la firma guardada se lee igual sobre cualquier pantalla
    const pad = new SignaturePad(canvas, { backgroundColor: 'rgb(255, 255, 255)' })
    pad.addEventListener('endStroke', () => setConTrazo(!pad.isEmpty()))
    padRef.current = pad
    return () => pad.off()
  }, [])

  const borrarFirma = () => {
    padRef.current?.clear()
    setConTrazo(false)
  }

  const elegir = (valor: boolean) => {
    setEsGanador(valor)
    // Quien retira es el ganador: su nombre ya se sabe, y se puede corregir
    if (valor && !nombre.trim()) setNombre(draw.winnerName)
  }

  const listo = esGanador !== null && nombre.trim() !== '' && documento.trim() !== '' && conTrazo

  const handleConfirm = async () => {
    if (!listo || !canvasRef.current) return
    const signature = await firmaPng(canvasRef.current)
    handOver.mutate(
      {
        prizeId: prize.id,
        body: {
          receiverName: nombre.trim(),
          receiverIdentityDocument: documento.trim(),
          receiverIsWinner: esGanador,
          signature,
        },
      },
      { onSuccess: onClose },
    )
  }

  const firmaError = fieldError(handOver.error, 'Signature')

  return (
    <ConfirmDialog
      title="Registrar entrega"
      confirmLabel="Registrar entrega"
      pending={handOver.isPending}
      confirmDisabled={!listo || !canDraw.allowed}
      error={handOver.error}
      onConfirm={() => void handleConfirm()}
      onCancel={onClose}
    >
      <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 dark:border-slate-700 dark:bg-slate-800/60">
        <p className="font-bold text-slate-900 dark:text-white">{prize.name}</p>
        <p>
          Ganador: <strong>{draw.winnerName}</strong>
          {draw.winnerCompany && ` · ${draw.winnerCompany}`}
        </p>
        <p className="font-mono text-[11px] text-slate-500">{draw.redemptionCode}</p>
      </div>
      {!canDraw.allowed && <p className="text-rose-700 dark:text-rose-400">{canDraw.title}</p>}

      <fieldset className="space-y-1.5">
        <legend className="mb-1 font-bold text-slate-700 dark:text-slate-200">
          ¿Quien retira es el ganador?
        </legend>
        <label className="flex cursor-pointer items-center gap-2">
          <input
            type="radio"
            name="receptor-es-ganador"
            checked={esGanador === true}
            onChange={() => elegir(true)}
          />
          Sí, es {draw.winnerName}
        </label>
        <label className="flex cursor-pointer items-center gap-2">
          <input
            type="radio"
            name="receptor-es-ganador"
            checked={esGanador === false}
            onChange={() => elegir(false)}
          />
          No, lo retira otra persona en su nombre
        </label>
      </fieldset>

      <Field label="Nombre de quien retira" error={fieldError(handOver.error, 'ReceiverName')}>
        <TextInput
          value={nombre}
          maxLength={200}
          onChange={(e) => setNombre(e.target.value)}
          placeholder="Nombre completo"
        />
      </Field>
      <Field
        label="Documento de identidad de quien retira"
        error={fieldError(handOver.error, 'ReceiverIdentityDocument')}
      >
        <TextInput
          value={documento}
          maxLength={100}
          onChange={(e) => setDocumento(e.target.value)}
          placeholder="Cédula o pasaporte, tal como se vio"
        />
      </Field>

      <div className="space-y-1">
        <div className="flex items-center justify-between">
          <span className="font-bold text-slate-700 dark:text-slate-200">
            Firma de quien retira
          </span>
          <button
            type="button"
            onClick={borrarFirma}
            className="cursor-pointer text-[11px] font-semibold text-blue-600 hover:underline dark:text-blue-400"
          >
            Borrar firma
          </button>
        </div>
        <canvas
          ref={canvasRef}
          aria-label="Lienzo de firma"
          className="h-36 w-full touch-none rounded-lg border border-slate-300 bg-white dark:border-slate-600"
        />
        {firmaError ? (
          <p className="text-red-600">{firmaError}</p>
        ) : (
          !conTrazo && <p className="text-slate-500">Firme dentro del recuadro para confirmar.</p>
        )}
      </div>
    </ConfirmDialog>
  )
}
