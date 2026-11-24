import type { DoorEventResponse } from '@/api/types'

/**
 * El evento elegido en este terminal, como comodidad del dispositivo (spec convocatorias/10 §4.1).
 * Sin `localStorage` (modo privado, datos borrados) el terminal solo vuelve a preguntar.
 */
const STORAGE_KEY = 'sorteo360.scanner.eventId'

/** El evento guardado, si sigue en la ventana de la puerta; si no, hay que volver a elegir. */
export function rememberedEvent(
  doorEvents: DoorEventResponse[],
  storedId: string | null,
): DoorEventResponse | null {
  return doorEvents.find((e) => e.id === storedId) ?? null
}

export function readStoredEventId(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

export function storeEventId(eventId: string) {
  try {
    localStorage.setItem(STORAGE_KEY, eventId)
  } catch {
    // Sin almacenamiento, el terminal pregunta en la próxima carga
  }
}
