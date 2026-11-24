import { useSyncExternalStore } from 'react'

/**
 * Cómo se llaman, en este sorteo, los datos del colaborador: el "ID de empleado" puede ser la
 * "Cédula" y el "Departamento" la "Sede". Salen de los encabezados del archivo al cargar el padrón
 * y se pueden corregir ahí mismo.
 *
 * shortcut: se guardan en el navegador como la marca del sorteo (`setRaffleBranding`); si el
 * sorteo se maneja desde varios equipos, pasarlas al servicio junto a `displayFields`.
 */
export interface EtiquetasPadron {
  employeeCode: string
  department: string
  location: string
  company: string
}

export const ETIQUETAS_POR_DEFECTO: EtiquetasPadron = {
  employeeCode: 'ID de empleado',
  department: 'Departamento',
  location: 'Ubicación',
  company: 'Empresa',
}

const clave = (eventId: string) => `sorteo360.etiquetas.${eventId}`
const CAMBIO = 'sorteo360:etiquetas'
const cache = new Map<string, EtiquetasPadron>()

export function leerEtiquetas(eventId: string): EtiquetasPadron {
  const enCache = cache.get(eventId)
  if (enCache) return enCache
  let guardadas: Partial<EtiquetasPadron> = {}
  try {
    guardadas = JSON.parse(localStorage.getItem(clave(eventId)) ?? '{}') as Partial<EtiquetasPadron>
  } catch {
    // Un valor roto vuelve a los nombres por omisión.
  }
  const etiquetas = { ...ETIQUETAS_POR_DEFECTO }
  for (const k of Object.keys(etiquetas) as (keyof EtiquetasPadron)[]) {
    const v = guardadas[k]
    if (typeof v === 'string' && v.trim()) etiquetas[k] = v.trim()
  }
  cache.set(eventId, etiquetas)
  return etiquetas
}

export function guardarEtiquetas(eventId: string, etiquetas: Partial<EtiquetasPadron>): void {
  const limpias = Object.fromEntries(
    Object.entries(etiquetas).filter(([, v]) => typeof v === 'string' && v.trim()),
  )
  try {
    localStorage.setItem(clave(eventId), JSON.stringify(limpias))
  } catch {
    // Sin almacenamiento se quedan los nombres por omisión.
  }
  cache.delete(eventId)
  window.dispatchEvent(new Event(CAMBIO))
}

function suscribir(aviso: () => void) {
  const alCambiar = () => {
    cache.clear()
    aviso()
  }
  window.addEventListener(CAMBIO, alCambiar)
  window.addEventListener('storage', alCambiar)
  return () => {
    window.removeEventListener(CAMBIO, alCambiar)
    window.removeEventListener('storage', alCambiar)
  }
}

export function useEtiquetasPadron(eventId: string): EtiquetasPadron {
  return useSyncExternalStore(suscribir, () => leerEtiquetas(eventId))
}
