import { ApiError } from '@/api/client'

/** El primer mensaje del servidor para un campo, sin importar si la clave llega en camelCase o PascalCase. */
export function errorDeCampo(error: unknown, campo: string): string | undefined {
  if (!(error instanceof ApiError) || !error.fieldErrors) return undefined
  const clave = Object.keys(error.fieldErrors).find((k) => k.toLowerCase() === campo.toLowerCase())
  return clave ? error.fieldErrors[clave]?.[0] : undefined
}
