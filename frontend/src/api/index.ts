import { config } from '@/lib/config'
import type { ApiClient } from './client'
import { HttpApiClient } from './http'

/**
 * El cliente de datos. Se asigna en `iniciarApi()` antes del primer render (`main.tsx`): el mock
 * (con su semilla) se descarga solo en modo `mock`, así no pesa en el paquete de producción.
 */
export let api: ApiClient

export async function iniciarApi(): Promise<void> {
  api =
    config.apiMode === 'http'
      ? new HttpApiClient()
      : new (await import('./mock/mockClient')).MockApiClient()
}

export { ApiError } from './client'
export type { ApiClient } from './client'
