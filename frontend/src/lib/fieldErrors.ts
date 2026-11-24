import { ApiError } from '@/api/client'

/**
 * El mensaje que el servicio puso sobre un campo, o `undefined` si el rechazo no fue de
 * campo. Las claves llegan como las nombra el validador del servicio (`Company`,
 * `Contact.Email`), así que se comparan sin distinguir mayúsculas.
 *
 * El texto es el del servicio y no se traduce: una validación de campo no trae código
 * máquina, y su mensaje ya viene en español y dice qué tiene de malo ese valor.
 */
export function fieldError(error: unknown, campo: string): string | undefined {
  if (!(error instanceof ApiError) || !error.fieldErrors) return undefined
  const clave = Object.keys(error.fieldErrors).find((k) => k.toLowerCase() === campo.toLowerCase())
  return clave ? error.fieldErrors[clave]?.[0] : undefined
}
