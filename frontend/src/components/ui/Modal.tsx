import { useEffect, useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { Animate } from './Animate'

export function Modal({
  title,
  onClose,
  children,
  wide = false,
  open = true,
  width,
  showCloseButton = true,
  sheet = false,
  animated = false,
}: {
  title?: ReactNode
  onClose: () => void
  children: ReactNode
  wide?: boolean
  /** Compat con llamadores que controlan el montaje vía prop en lugar de `{cond && <Modal>}`. */
  open?: boolean
  /** Clase Tailwind `max-w-*` explícita; tiene prioridad sobre `wide`. */
  width?: string
  /** `true` por defecto. En `false` el formulario debe ofrecer su propio botón de Cancelar —
   * Escape y el clic en el fondo siguen cerrando igual. */
  showCloseButton?: boolean
  /** Sube desde abajo, pegado al borde, como una hoja en el teléfono. */
  sheet?: boolean
  /** El fondo aparece con un fundido y la caja con un zoom suave (o sube, si es `sheet`).
   * Apagado por defecto: hoy solo lo activa el módulo de Ferias. */
  animated?: boolean
}) {
  const titleId = useId()
  const dialogRef = useRef<HTMLDialogElement>(null)
  const onCloseRef = useRef(onClose)

  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (open && !dialog.open) {
      dialog.showModal()
    } else if (!open && dialog.open) {
      dialog.close()
    }
  }, [open])

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    const handleClose = () => {
      dialog.returnValue = ''
      onCloseRef.current()
    }
    dialog.addEventListener('close', handleClose)

    // Fallback para navegadores sin soporte nativo de `closedby="any"` (Safari).
    if (!('closedBy' in HTMLDialogElement.prototype)) {
      let pressedOutside = false
      const isOutside = (e: PointerEvent) => {
        const r = dialog.getBoundingClientRect()
        return (
          e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom
        )
      }
      const onPointerDown = (e: PointerEvent) => {
        pressedOutside = isOutside(e)
      }
      const onPointerUp = (e: PointerEvent) => {
        if (pressedOutside && isOutside(e)) dialog.close()
        pressedOutside = false
      }
      dialog.addEventListener('pointerdown', onPointerDown)
      dialog.addEventListener('pointerup', onPointerUp)
      return () => {
        dialog.removeEventListener('close', handleClose)
        dialog.removeEventListener('pointerdown', onPointerDown)
        dialog.removeEventListener('pointerup', onPointerUp)
      }
    }

    return () => {
      dialog.removeEventListener('close', handleClose)
    }
  }, [open])

  if (!open) return null

  return createPortal(
    <dialog
      ref={dialogRef}
      {...({ closedby: 'any' } as Record<string, unknown>)}
      aria-labelledby={title ? titleId : undefined}
      className={`m-0 w-full max-w-none bg-transparent p-0 backdrop:bg-slate-900/50 backdrop:backdrop-blur-xs open:flex ${
        sheet
          ? 'mt-auto mb-0 items-end justify-center'
          : 'mx-auto mt-auto mb-0 sm:my-auto items-end sm:items-center justify-center sm:p-4'
      } ${width ?? (wide ? 'sm:max-w-3xl' : 'sm:max-w-xl')}`}
    >
      <Animate
        variant={sheet ? 'fade-up' : 'zoom'}
        distance={24}
        duration={200}
        disabled={!animated}
        className={`w-full max-h-[92dvh] sm:max-h-[90vh] ${sheet ? 'rounded-t-3xl' : 'rounded-t-2xl sm:rounded-2xl'} bg-white shadow-2xl dark:bg-slate-900 dark:border dark:border-slate-800 flex flex-col`}
      >
        {/* Header con Título y Botón Cerrar */}
        <div className="flex items-center justify-between border-b border-slate-200/80 bg-white/95 px-5 py-3.5 dark:border-slate-800 dark:bg-slate-900/95 rounded-t-2xl shrink-0">
          <div id={titleId} className="flex flex-1 min-w-0 items-center gap-2">
            {typeof title === 'string' ? (
              <h3 className="font-sans text-base font-bold text-slate-900 dark:text-white">
                {title}
              </h3>
            ) : (
              title
            )}
          </div>
          {showCloseButton && (
            <button
              type="button"
              onClick={() => dialogRef.current?.close()}
              className="-mr-1.5 flex h-10 w-10 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-slate-800 dark:hover:text-slate-200 transition-colors cursor-pointer"
              aria-label="Cerrar"
            >
              <X size={18} />
            </button>
          )}
        </div>

        {/* Contenido con Scroll vertical automático */}
        <div className="overflow-y-auto overscroll-contain px-5 py-4 custom-scrollbar">
          {children}
        </div>
      </Animate>
    </dialog>,
    document.body,
  )
}
