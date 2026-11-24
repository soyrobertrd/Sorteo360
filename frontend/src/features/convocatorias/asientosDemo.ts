import { useState } from 'react'
import type { EventoListItem } from '@/api/types'
import { invitadosFor, type Invitado } from './mockParticipants'
import { puedeSentarse } from './mesas'

/**
 * Invitados de prueba para ver los asientos en un ambiente sin registros reales: los mismos que
 * genera el mock, todos inscritos y uno de cada tres ya con check-in.
 */
export function invitadosDemo(evento: EventoListItem): Invitado[] {
  return invitadosFor({ ...evento, externalUrl: evento.externalUrl || 'demo' }).map((i, n) =>
    puedeSentarse(i) ? i : { ...i, status: n % 3 === 0 ? 'CHECKED_IN' : 'REGISTERED' },
  )
}

/** Un valor de prueba de este navegador, por clave; `null` cuando no hay. */
function useValorLocal(clave: string) {
  const leer = () => {
    try {
      return localStorage.getItem(clave)
    } catch {
      return null
    }
  }
  const [valor, setValor] = useState(leer)
  const cambiar = (nuevo: string | null) => {
    try {
      if (nuevo) localStorage.setItem(clave, nuevo)
      else localStorage.removeItem(clave)
    } catch {
      // Sin almacenamiento la elección dura lo que la pestaña.
    }
    setValor(nuevo)
  }
  return [valor, cambiar] as const
}

/**
 * Si este navegador pidió ver el evento con invitados de prueba.
 * shortcut: la elección vive en `localStorage` y nunca llega al servicio; desaparece cuando el
 * ambiente de desarrollo tenga registros sembrados en convocatorias-service.
 */
export function useInvitadosDemo(eventoId: string) {
  const [valor, cambiar] = useValorLocal(`sorteo360.asientos.demo.${eventoId}`)
  return { activo: valor === '1', cambiar: (activo: boolean) => cambiar(activo ? '1' : null) }
}
