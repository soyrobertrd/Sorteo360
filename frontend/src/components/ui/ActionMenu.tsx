import { useEffect, useLayoutEffect, useRef, useState, type ReactNode, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'

/**
 * Menú de acciones posicionado por `fixed` respecto al botón que lo abre, en un
 * portal a `document.body` — no vive dentro del flujo de la fila/tabla que lo
 * dispara. Los menús posicionados con `absolute` dentro de un contenedor con
 * `overflow-y-auto` (como el cuerpo de `Modal`) empujan el `scrollHeight` de ese
 * contenedor y terminan renderizando fuera de la parte visible, obligando a
 * scrollear para verlos completos. Este componente mide el espacio disponible y
 * abre hacia arriba si no entra hacia abajo, así siempre aparece completo sin
 * necesidad de scroll.
 */
export function ActionMenu({
  open,
  onClose,
  anchorRef,
  align = 'end',
  matchAnchorWidth = false,
  children,
  className = '',
}: {
  open: boolean
  onClose: () => void
  anchorRef: RefObject<HTMLElement | null>
  /** Borde del botón contra el que se alinea horizontalmente el menú. */
  align?: 'start' | 'end'
  /** El menú mide lo mismo que el botón que lo abre (la lista de un select). */
  matchAnchorWidth?: boolean
  children: ReactNode
  className?: string
}) {
  const menuRef = useRef<HTMLDivElement>(null)
  const [position, setPosition] = useState<{ top: number; left: number } | null>(null)

  // El botón que abre el menú no está dentro de `menuRef` (el menú vive en un
  // portal, fuera de la fila), así que un click-outside genérico lo trataría
  // como "afuera" y el propio toggle del botón jamás lo dejaría reabrir.
  useEffect(() => {
    if (!open) return
    const handleClick = (e: MouseEvent | TouchEvent) => {
      const target = e.target as Node
      if (menuRef.current?.contains(target) || anchorRef.current?.contains(target)) return
      onClose()
    }
    document.addEventListener('mousedown', handleClick)
    document.addEventListener('touchstart', handleClick)
    return () => {
      document.removeEventListener('mousedown', handleClick)
      document.removeEventListener('touchstart', handleClick)
    }
  }, [open, onClose, anchorRef])

  // Se mide antes de pintar (useLayoutEffect) para no mostrar el menú en la
  // posición equivocada ni un instante.
  useLayoutEffect(() => {
    if (!open) {
      setPosition(null)
      return
    }
    const anchor = anchorRef.current
    const menu = menuRef.current
    if (!anchor || !menu) return

    const anchorRect = anchor.getBoundingClientRect()
    // El ancho se fija antes de medir el alto: una lista más angosta puede partir las opciones.
    if (matchAnchorWidth) menu.style.width = `${anchorRect.width}px`
    const menuRect = menu.getBoundingClientRect()
    const margin = 6

    const cabeAbajo = anchorRect.bottom + margin + menuRect.height <= window.innerHeight
    const top = cabeAbajo
      ? anchorRect.bottom + margin
      : Math.max(margin, anchorRect.top - margin - menuRect.height)

    let left = align === 'end' ? anchorRect.right - menuRect.width : anchorRect.left
    left = Math.min(Math.max(left, margin), window.innerWidth - menuRect.width - margin)

    setPosition({ top, left })
  }, [open, anchorRef, align, matchAnchorWidth])

  // Al desplazar la página el menú sigue a su botón; solo se cierra si el botón sale de la vista.
  // Cerrarlo con cualquier desplazamiento lo cerraba justo al abrirlo cuando el navegador movía la
  // página para mostrar el botón (un menú arriba con la pestaña de abajo a la vista).
  useLayoutEffect(() => {
    if (!open) return
    const handleScroll = (e: Event) => {
      // Desplazar la propia lista del menú (muchas opciones) no lo mueve.
      if (menuRef.current?.contains(e.target as Node)) return
      const anchor = anchorRef.current
      const menu = menuRef.current
      if (!anchor || !menu) return
      const a = anchor.getBoundingClientRect()
      if (a.bottom < 0 || a.top > window.innerHeight) {
        onClose()
        return
      }
      const m = menu.getBoundingClientRect()
      const margin = 6
      const top =
        a.bottom + margin + m.height <= window.innerHeight
          ? a.bottom + margin
          : Math.max(margin, a.top - margin - m.height)
      let left = align === 'end' ? a.right - m.width : a.left
      left = Math.min(Math.max(left, margin), window.innerWidth - m.width - margin)
      setPosition({ top, left })
    }
    const handleResize = () => onClose()
    window.addEventListener('scroll', handleScroll, true)
    window.addEventListener('resize', handleResize)
    return () => {
      window.removeEventListener('scroll', handleScroll, true)
      window.removeEventListener('resize', handleResize)
    }
  }, [open, onClose, anchorRef, align])

  if (!open) return null

  return createPortal(
    <div
      ref={menuRef}
      style={{
        position: 'fixed',
        top: position?.top ?? 0,
        left: position?.left ?? 0,
        visibility: position ? 'visible' : 'hidden',
      }}
      className={cn(
        'z-50 w-52 rounded-xl border border-slate-200/80 bg-white p-1.5 text-left shadow-[0_16px_40px_-12px_rgb(15_23_42/0.22),0_2px_6px_rgb(15_23_42/0.05)] dark:border-slate-700/80 dark:bg-slate-900',
        className,
      )}
    >
      {children}
    </div>,
    document.body,
  )
}
