import { useSyncExternalStore } from 'react'
import { createStore } from './store'

/**
 * Los invitados de un evento privado: solo ellos pueden registrarse. Son las personas a las que
 * se envió alguna campaña del evento, y se ajustan a mano: agregar a alguien para que pueda
 * registrarse sin el enlace de la campaña, quitarlo, o enviarle la invitación a él solo.
 *
 * shortcut: los ajustes manuales viven en `localStorage` de este navegador, solo para la demo;
 * cuando convocatorias-service guarde la lista (REQUERIMIENTOS_BACKEND.md §13) se lee de la API.
 */

export interface EntradaListaBlanca {
  email: string
  nombre: string
  empresa?: string | null
  /** `convocatoria`: vino de una campaña. `manual`: alguien la agregó a mano. */
  fuente: 'convocatoria' | 'manual'
}

interface Ajustes {
  agregados: { email: string; nombre: string; empresa?: string | null }[]
  /** Correos de la convocatoria que se sacaron a mano. */
  quitados: string[]
  /** Correo -> cuándo se le envió la invitación individual desde esta lista. */
  enviadas?: Record<string, string>
}

const SIN_AJUSTES: Ajustes = { agregados: [], quitados: [], enviadas: {} }

const ajustesStore = createStore<Record<string, Ajustes>>('sorteo360.convocatorias.listaBlanca', {})

const clave = (email: string) => email.trim().toLowerCase()

export function useAjustesListaBlanca(eventoId: string): Ajustes {
  const todos = useSyncExternalStore(ajustesStore.subscribe, ajustesStore.get)
  return todos[eventoId] ?? SIN_AJUSTES
}

/** La lista vigente: la convocatoria, sin los quitados, más los agregados a mano. */
export function listaBlanca(
  base: { email: string; nombre: string; empresa?: string | null }[],
  ajustes: Ajustes,
): EntradaListaBlanca[] {
  const quitados = new Set(ajustes.quitados.map(clave))
  const vistos = new Set<string>()
  const lista: EntradaListaBlanca[] = []
  for (const b of base) {
    const k = clave(b.email)
    if (quitados.has(k) || vistos.has(k)) continue
    vistos.add(k)
    lista.push({ ...b, fuente: 'convocatoria' })
  }
  for (const a of ajustes.agregados) {
    const k = clave(a.email)
    if (vistos.has(k)) continue
    vistos.add(k)
    lista.push({ ...a, fuente: 'manual' })
  }
  return lista
}

function actualizar(eventoId: string, cambio: (prev: Ajustes) => Ajustes) {
  ajustesStore.set((prev) => ({ ...prev, [eventoId]: cambio(prev[eventoId] ?? SIN_AJUSTES) }))
}

/** Agrega un correo; si era de la convocatoria y se había quitado, vuelve a la lista. */
export function agregarAListaBlanca(
  eventoId: string,
  persona: { email: string; nombre: string; empresa?: string | null },
) {
  const k = clave(persona.email)
  actualizar(eventoId, (prev) => ({
    ...prev,
    quitados: prev.quitados.filter((q) => clave(q) !== k),
    agregados: prev.agregados.some((a) => clave(a.email) === k)
      ? prev.agregados
      : [...prev.agregados, { ...persona, email: persona.email.trim() }],
  }))
}

/** Quita un correo: uno agregado a mano se borra; uno de la convocatoria queda excluido. */
export function quitarDeListaBlanca(eventoId: string, entrada: EntradaListaBlanca) {
  const k = clave(entrada.email)
  actualizar(eventoId, (prev) =>
    entrada.fuente === 'manual'
      ? { ...prev, agregados: prev.agregados.filter((a) => clave(a.email) !== k) }
      : { ...prev, quitados: [...prev.quitados, entrada.email] },
  )
}

/** Devuelve a la lista a alguien de la convocatoria que se había quitado. */
export function restaurarEnListaBlanca(eventoId: string, email: string) {
  const k = clave(email)
  actualizar(eventoId, (prev) => ({
    ...prev,
    quitados: prev.quitados.filter((q) => clave(q) !== k),
  }))
}

/** Fuera de React: el mock del portal decide con esto si un correo puede registrarse. */
export function correoEnListaBlanca(eventoId: string, email: string, base: string[]): boolean {
  const ajustes = ajustesStore.get()[eventoId] ?? SIN_AJUSTES
  return listaBlanca(
    base.map((b) => ({ email: b, nombre: '' })),
    ajustes,
  ).some((e) => clave(e.email) === clave(email))
}

/**
 * Envía la invitación del evento a una persona de la lista.
 * shortcut: en la demo solo anota la fecha; cuando exista el envío individual en
 * convocatorias-service (REQUERIMIENTOS_BACKEND.md §13) sale el correo con su enlace.
 */
export function enviarInvitacion(eventoId: string, email: string) {
  actualizar(eventoId, (prev) => ({
    ...prev,
    enviadas: { ...prev.enviadas, [clave(email)]: new Date().toISOString() },
  }))
}

export const invitacionEnviada = (ajustes: Ajustes, email: string) =>
  ajustes.enviadas?.[clave(email)] ?? null
