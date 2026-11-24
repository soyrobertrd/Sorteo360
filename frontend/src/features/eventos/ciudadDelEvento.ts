import { ApiError } from '@/api'
import type { CiudadRequest, CiudadResponse } from '@/api/types'

/** Lo que el formulario del evento edita de su ciudad. */
export interface UbicacionDelFormulario {
  pais: string
  ciudad: string
  zona: string
}

/** El PATCH de la ciudad del evento: cuál, y la ciudad entera tal como debe quedar. */
export type CambioDeCiudad = { ciudadId: string; body: CiudadRequest }

/**
 * El PATCH de la ciudad que deja al servicio como el formulario. El PATCH reemplaza la ciudad
 * entera (ADR-0009), así que la fecha y el orden, que el formulario no muestra, viajan tal como
 * vinieron: omitirlos los borraría. Ausente cuando nada cambió, y entonces no hay PATCH.
 */
export function cambioDeCiudad(
  guardada: CiudadResponse,
  valores: UbicacionDelFormulario,
): CambioDeCiudad | undefined {
  const body: CiudadRequest = {
    name: valores.ciudad.trim(),
    country: valores.pais.trim(),
    date: guardada.date ?? null,
    displayOrder: guardada.displayOrder ?? null,
    timeZone: valores.zona.trim(),
  }
  const igual =
    body.name === guardada.name &&
    body.country === guardada.country &&
    body.timeZone === guardada.timeZone
  return igual ? undefined : { ciudadId: guardada.id, body }
}

/**
 * El evento se guardó y su ciudad no: son dos PATCH. Conserva el código del rechazo, para que
 * `TIME_ZONE_UNKNOWN` siga marcando el selector de la zona. Volver a guardar reenvía el evento,
 * que no cambia nada, y otra vez la ciudad, que sigue distinta de la guardada.
 */
export class EventoSinCiudadError extends ApiError {
  constructor(cause: unknown) {
    const rechazo = cause instanceof ApiError ? cause : undefined
    const detalle = rechazo?.message ?? 'No se pudo completar la operación.'
    super(
      `El evento se guardó, pero su ciudad no: ${detalle} Vuelve a guardar para corregirla.`,
      rechazo?.status ?? 0,
      rechazo?.fieldErrors,
      rechazo?.code,
    )
  }
}
