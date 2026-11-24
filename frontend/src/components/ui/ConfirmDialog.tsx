import type { ReactNode } from 'react'
import { Loader2 } from 'lucide-react'
import { Button } from './Button'
import { ErrorNote } from './Feedback'
import { Modal } from './Modal'

/**
 * Pregunta antes de una acción que cuesta deshacer. El cuerpo dice qué va a pasar; el botón
 * de confirmar repite el verbo de la acción, nunca "Aceptar".
 */
export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  cancelLabel = 'Volver',
  destructive = false,
  pending = false,
  confirmDisabled = false,
  error,
  onConfirm,
  onCancel,
}: {
  title: string
  children: ReactNode
  confirmLabel: string
  cancelLabel?: string
  /** La acción quita o detiene algo: el botón de confirmar va en rojo. */
  destructive?: boolean
  pending?: boolean
  /** Falta algo para poder confirmar; el cuerpo dice qué. */
  confirmDisabled?: boolean
  /** El fallo de la acción, para que se vea sin cerrar la pregunta. */
  error?: unknown
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <Modal title={title} onClose={onCancel} width="max-w-md">
      <div className="space-y-4 text-xs text-slate-600 dark:text-slate-300">
        <div className="space-y-2 leading-relaxed">{children}</div>
        {error != null && <ErrorNote error={error} />}
        <div className="flex justify-end gap-2.5 border-t border-slate-100 pt-4 dark:border-slate-800">
          <Button
            type="button"
            variant="outline"
            onClick={onCancel}
            disabled={pending}
            className="cursor-pointer text-xs font-bold"
          >
            {cancelLabel}
          </Button>
          <Button
            type="button"
            onClick={onConfirm}
            disabled={pending || confirmDisabled}
            className={`cursor-pointer text-xs font-bold text-white ${
              destructive ? 'bg-rose-700 hover:bg-rose-800' : 'bg-brand-deep hover:bg-brand-deep/90'
            }`}
          >
            {pending && <Loader2 size={14} className="mr-1.5 animate-spin" />}
            {confirmLabel}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
