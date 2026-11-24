import { useEffect, type RefObject } from 'react'

/**
 * Hook universal para cerrar menús desplegables, popups y dropdowns al hacer clic fuera (en cualquier espacio en blanco).
 */
export function useClickOutside<T extends HTMLElement = HTMLElement>(
  ref: RefObject<T | null>,
  handler: () => void,
  active: boolean = true,
) {
  useEffect(() => {
    if (!active) return

    const listener = (event: MouseEvent | TouchEvent) => {
      const el = ref.current
      if (!el || el.contains(event.target as Node)) {
        return
      }
      handler()
    }

    document.addEventListener('mousedown', listener)
    document.addEventListener('touchstart', listener)

    return () => {
      document.removeEventListener('mousedown', listener)
      document.removeEventListener('touchstart', listener)
    }
  }, [ref, handler, active])
}
