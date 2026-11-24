import { useEffect, useSyncExternalStore } from 'react'

/**
 * Tema claro/oscuro compartido por los tres layouts (AppShell, PortalHeader,
 * PublicLayout), que antes mantenían cada uno su propio `useState` sin
 * sincronizar entre sí ni sobrevivir a un reload. Vive en `localStorage` de
 * este origen, con el mismo patrón de store que `features/convocatorias/store.ts`.
 */

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'sorteo360.theme'

function prefersDark(): boolean {
  return (
    typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches
  )
}

function readFromStorage(): Theme {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw === 'light' || raw === 'dark') return raw
  } catch {
    // Sin acceso a localStorage: se sigue con el default de sistema.
  }
  return prefersDark() ? 'dark' : 'light'
}

let state: Theme = readFromStorage()
const listeners = new Set<() => void>()

function notify() {
  listeners.forEach((listener) => listener())
}

function applyToDocument(theme: Theme) {
  document.documentElement.classList.toggle('dark', theme === 'dark')
}

if (typeof window !== 'undefined') {
  applyToDocument(state)
  window.addEventListener('storage', (e) => {
    if (e.key === STORAGE_KEY) {
      state = readFromStorage()
      applyToDocument(state)
      notify()
    }
  })
}

function setTheme(theme: Theme) {
  state = theme
  applyToDocument(theme)
  try {
    localStorage.setItem(STORAGE_KEY, theme)
  } catch {
    // Cuota excedida u origen sin acceso: el tema sigue activo para esta pestaña.
  }
  notify()
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function useTheme(): [Theme, () => void] {
  const theme = useSyncExternalStore(subscribe, () => state)
  // Idempotente: aplicar la misma clase varias veces (una por componente que
  // use el hook en la misma pantalla) no tiene efecto visible ni de rendimiento.
  useEffect(() => {
    applyToDocument(theme)
  }, [theme])
  const toggle = () => setTheme(state === 'dark' ? 'light' : 'dark')
  return [theme, toggle]
}
