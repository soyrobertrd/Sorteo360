import { useId, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'

type Side = 'top' | 'bottom' | 'left' | 'right'

/**
 * Tooltip con estilo del sistema (fondo oscuro, texto blanco), posicionado por
 * `fixed` en un portal para no recortarse dentro de contenedores con `overflow`.
 * Sustituye a los `title="…"` nativos (lentos, sin tema, sin control de estilo).
 *
 * Aparece con hover y con foco de teclado. Si `content` está vacío, no hace nada.
 */
export function Tooltip({
  content,
  children,
  side = 'top',
  className,
}: {
  content: ReactNode
  children: ReactNode
  side?: Side
  className?: string
}) {
  const id = useId()
  const triggerRef = useRef<HTMLSpanElement>(null)
  const [coords, setCoords] = useState<{ top: number; left: number } | null>(null)

  const show = () => {
    const el = triggerRef.current
    if (!el) return
    const r = el.getBoundingClientRect()
    const gap = 8
    setCoords(
      side === 'bottom'
        ? { top: r.bottom + gap, left: r.left + r.width / 2 }
        : side === 'left'
          ? { top: r.top + r.height / 2, left: r.left - gap }
          : side === 'right'
            ? { top: r.top + r.height / 2, left: r.right + gap }
            : { top: r.top - gap, left: r.left + r.width / 2 },
    )
  }
  const hide = () => setCoords(null)

  if (content == null || content === '') return <>{children}</>

  const transform =
    side === 'top'
      ? 'translate(-50%, -100%)'
      : side === 'bottom'
        ? 'translate(-50%, 0)'
        : side === 'left'
          ? 'translate(-100%, -50%)'
          : 'translate(0, -50%)'

  return (
    <>
      <span
        ref={triggerRef}
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocus={show}
        onBlur={hide}
        aria-describedby={coords ? id : undefined}
        className="inline-flex"
      >
        {children}
      </span>
      {coords &&
        createPortal(
          <span
            id={id}
            role="tooltip"
            style={{ position: 'fixed', top: coords.top, left: coords.left, transform, zIndex: 60 }}
            className={cn(
              'pointer-events-none max-w-xs rounded-lg bg-slate-900 px-2.5 py-1.5 text-[11px] font-semibold leading-snug text-white shadow-lg dark:bg-slate-700',
              className,
            )}
          >
            {content}
          </span>,
          document.body,
        )}
    </>
  )
}
