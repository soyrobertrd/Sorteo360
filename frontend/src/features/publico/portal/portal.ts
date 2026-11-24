import { useLocation } from 'react-router-dom'
import { ApiError } from '@/api'
import type { ApiErrorCode } from '@/api/types'
import { apiErrorCodeLabel } from '@/lib/labels'

/**
 * Un valor del fragmento (`#t=…`, `#u=…`). El token viaja ahí y no en la cadena de consulta
 * porque el navegador no manda el fragmento al servidor (spec convocatorias/06 §2.1).
 */
export function useFragmentParam(key: string): string | null {
  return fragmentParam(useLocation().hash, key)
}

/** El valor de `key` en un fragmento como `#t=…`; vacío o ausente es `null`. */
export function fragmentParam(hash: string, key: string): string | null {
  const value = new URLSearchParams(hash.replace(/^#/, '')).get(key)
  return value?.trim() || null
}

export function hasCode(error: unknown, code: ApiErrorCode): boolean {
  return error instanceof ApiError && error.code === code
}

/**
 * El texto de un fallo del portal. Un 429 dice que se espere aunque llegue sin código (lo puede
 * cortar el proxy antes que el servicio); el resto ya viene traducido por su código.
 */
export function portalErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.status === 429 ? apiErrorCodeLabel.RATE_LIMITED : error.message
  }
  return 'No se pudo completar la operación. Intenta nuevamente.'
}

/** El error de un campo del validator (`FirstName`, `Email`…), sin distinguir mayúsculas. */
export function fieldError(error: unknown, field: string): string | undefined {
  if (!(error instanceof ApiError) || !error.fieldErrors) return undefined
  const key = Object.keys(error.fieldErrors).find((k) => k.toLowerCase() === field.toLowerCase())
  return key ? error.fieldErrors[key]?.[0] : undefined
}
