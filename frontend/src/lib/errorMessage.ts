import { ApiError } from '@/api'

/** Un objeto plano con `message` — como el que arma `validateHeicFile`, no un `Error`. */
function hasStringMessage(value: unknown): value is { message: string } {
  return (
    typeof value === 'object' &&
    value !== null &&
    'message' in value &&
    typeof (value as { message: unknown }).message === 'string'
  )
}

/** El texto que se le muestra a quien usa la pantalla por un fallo cualquiera. */
export function errorMessage(error: unknown): string {
  return error instanceof ApiError
    ? error.message
    : error instanceof Error
      ? `No se pudo completar la operación (${error.message}).`
      : hasStringMessage(error)
        ? error.message
        : 'No se pudo completar la operación.'
}
