import { ApiError } from '@/api/client'
import { NETWORK_ERROR_STATUS } from '@/api/http'

/**
 * Cola de check-ins leídos sin conexión. Si el wifi de la puerta se cae, la lectura se guarda en
 * este navegador y se manda sola cuando vuelve la red, en el orden en que se leyó.
 */
export interface CheckinPendiente {
  codigo: string
  leidoEn: string
}

const clave = (eventId: string) => `sorteo360.checkin.pendientes.${eventId}`

export function leerCola(eventId: string): CheckinPendiente[] {
  try {
    const raw = localStorage.getItem(clave(eventId))
    const lista: unknown = raw ? JSON.parse(raw) : []
    return Array.isArray(lista) ? (lista as CheckinPendiente[]) : []
  } catch {
    return []
  }
}

function guardarCola(eventId: string, cola: CheckinPendiente[]) {
  try {
    if (cola.length) localStorage.setItem(clave(eventId), JSON.stringify(cola))
    else localStorage.removeItem(clave(eventId))
  } catch {
    // Sin almacenamiento (modo privado): la lectura se pierde, igual que sin cola.
  }
}

/** Agrega una lectura; el mismo código dos veces cuenta una sola. */
export function encolar(eventId: string, codigo: string, ahora = new Date()): CheckinPendiente[] {
  const cola = leerCola(eventId)
  if (!cola.some((p) => p.codigo === codigo)) cola.push({ codigo, leidoEn: ahora.toISOString() })
  guardarCola(eventId, cola)
  return cola
}

/** El fallo fue de red (no hubo respuesta), no un rechazo del servidor. */
export function esSinConexion(error: unknown): boolean {
  return (
    (error instanceof ApiError && error.status === NETWORK_ERROR_STATUS) ||
    (typeof navigator !== 'undefined' && navigator.onLine === false)
  )
}

/**
 * Manda la cola en orden. Se detiene en el primer fallo de red (siguen pendientes ese y los
 * siguientes); un rechazo del servidor (código desconocido) se descarta y se informa.
 */
export async function vaciarCola(
  eventId: string,
  enviar: (codigo: string) => Promise<unknown>,
): Promise<{ enviados: number; rechazados: string[]; pendientes: number }> {
  const cola = leerCola(eventId)
  let enviados = 0
  const rechazados: string[] = []
  while (cola.length) {
    const [primero] = cola
    try {
      await enviar(primero.codigo)
      enviados++
    } catch (error) {
      if (esSinConexion(error)) break
      rechazados.push(primero.codigo)
    }
    cola.shift()
    guardarCola(eventId, cola)
  }
  return { enviados, rechazados, pendientes: cola.length }
}
