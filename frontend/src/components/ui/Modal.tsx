import { useEffect, useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { Animate } from './Animate'
import { useDialogFocus } from './useDialogFocus'

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
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  // Foco inicial, Tab que no sale del diálogo y vuelta a quien lo abrió.
  useDialogFocus(panelRef, open)

  if (!open) return null

  // Se monta en <body>: un diálogo con su propio <form> abierto desde otro formulario
  // quedaría anidado en el DOM, y Chrome no deja subir el submit del form interno hasta
  // la raíz de React, así que el navegador recargaba la página en vez de guardar.
  return createPortal(
    <Animate
      variant="fade"
      duration={200}
      disabled={!animated}
      className={`fixed inset-0 z-50 flex justify-center bg-slate-900/50 backdrop-blur-xs ${
        // En el teléfono todo diálogo sube desde abajo, al alcance del pulgar.
        sheet ? 'items-end' : 'items-end sm:items-center sm:p-4'
      }`}
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      <Animate
        variant={sheet ? 'fade-up' : 'zoom'}
        distance={24}
        duration={200}
        disabled={!animated}
        ref={panelRef}
        className={`w-full max-h-[92dvh] sm:max-h-[90vh] ${sheet ? 'rounded-t-3xl' : 'rounded-t-2xl sm:rounded-2xl'} bg-white shadow-2xl dark:bg-slate-900 dark:border dark:border-slate-800 flex flex-col ${
          width ?? (wide ? 'sm:max-w-3xl' : 'sm:max-w-xl')
        }`}
        onClick={(e) => e.stopPropagation()}
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
              onClick={onClose}
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
    </Animate>,
    document.body,
  )
}
