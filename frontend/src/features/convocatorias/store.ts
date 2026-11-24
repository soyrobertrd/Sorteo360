import { useSyncExternalStore } from 'react'
import type { Invitado, Campana, ParticipantStatus } from './mockParticipants'

/**
 * Estado compartido entre rutas que no comparten árbol de componentes (portal
 * público de registro ↔ módulo administrativo de convocatorias) y entre
 * pestañas del mismo navegador — así el presentador puede tener el portal
 * público abierto en una pestaña y el panel de Convocatorias en otra durante
 * una demo. Es estado de demostración que el servicio no guarda: se respalda
 * en `localStorage` de este origen únicamente, nunca en un servidor real.
 */
export function createStore<T>(key: string, initial: T) {
  const readFromStorage = (): T => {
    try {
      const raw = localStorage.getItem(key)
      return raw ? (JSON.parse(raw) as T) : initial
    } catch {
      return initial
    }
  }

  let state = readFromStorage()
  const listeners = new Set<() => void>()

  const notify = () => listeners.forEach((listener) => listener())

  if (typeof window !== 'undefined') {
    window.addEventListener('storage', (e) => {
      if (e.key === key) {
        state = readFromStorage()
        notify()
      }
    })
  }

  return {
    get: () => state,
    set: (updater: (prev: T) => T) => {
      state = updater(state)
      try {
        localStorage.setItem(key, JSON.stringify(state))
      } catch {
        // Cuota de localStorage excedida u origen sin acceso: el estado sigue
        // válido en memoria para esta pestaña, solo no persiste ni se sincroniza.
      }
      notify()
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
  }
}

function useStore<T>(store: ReturnType<typeof createStore<T>>): T {
  return useSyncExternalStore(store.subscribe, store.get)
}

// ---------- Registros de invitados hechos desde el portal público (sección 3) ----------

const registrosStore = createStore<Invitado[]>('sorteo360.convocatorias.registrosPublicos', [])

export function addRegistro(registro: Invitado) {
  registrosStore.set((prev) => [registro, ...prev])
}

/** Fuera de React: el mock de la API los lee como inscripciones sin invitación. */
export function registrosPublicosGuardados(): Invitado[] {
  return registrosStore.get()
}

export function useRegistrosPublicos(): Invitado[] {
  return useStore(registrosStore)
}

// ---------- Campañas simuladas disparadas desde otras pantallas (sección 2) ----------
// Solo en modo mock: la tabla de Campañas las suma a las del servicio. Nueva campaña ya no
// escribe aquí; crea la campaña en el servicio (o en el mock de la API).

const campanasExtraStore = createStore<Campana[]>('sorteo360.convocatorias.campanasExtra', [])

export function addCampanaExtra(campana: Campana) {
  campanasExtraStore.set((prev) => [campana, ...prev])
}

export function useCampanasExtra(): Campana[] {
  return useStore(campanasExtraStore)
}

// ---------- Check-ins hechos en la pantalla de Check-in ----------
// Estado por invitado (`id` -> estado) que el servicio todavía no guarda. Lo comparten la
// pantalla de Check-in (que lo escribe) y el listado de eventos (que calcula la ocupación).

const checkinsStore = createStore<Record<string, ParticipantStatus>>(
  'sorteo360.convocatorias.checkins',
  {},
)

export function setEstadoInvitado(invitadoId: string, status: ParticipantStatus) {
  checkinsStore.set((prev) => ({ ...prev, [invitadoId]: status }))
}

export function useCheckinsOverrides(): Record<string, ParticipantStatus> {
  return useStore(checkinsStore)
}
