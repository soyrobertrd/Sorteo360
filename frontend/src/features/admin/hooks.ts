import { useMutation, useMutationState, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import type { FeatureSwitchResponse } from '@/api/types'
import { useAuth } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import type { SwitchKey } from './featureCatalogue'
import {
  ESTILOS_CLASICOS,
  estiloClasicoActivo,
  type EstiloClasicoKey,
  isSwitchEnabled,
  NO_STORED_SWITCHES,
  replaceRow,
  storedSwitchesFrom,
  withDeadline,
} from './featureSwitches'

const featureSwitchKeys = { todos: ['feature-switches'] as const }
const setFeatureSwitchKey = ['feature-switches', 'set'] as const

const CINCO_MINUTOS = 5 * 60_000

/**
 * Cuánto espera una ruta la primera lectura antes de abrirse igual (docs/adr/0039). Un crm-service
 * colgado no deja a nadie mirando un spinner, y uno sano contesta mucho antes.
 */
const PLAZO_DE_LECTURA = 3_000

interface SwitchVariables {
  key: SwitchKey | EstiloClasicoKey
  enabled: boolean
}

/**
 * Los interruptores guardados. Se leen una vez al entrar, al volver a la pestaña y cada cinco
 * minutos, así que lo que apaga un administrador llega a los demás sin recargar. El personal de la
 * puerta no los lee: no tiene `platform:staff`, no alcanza ningún módulo y recibiría 403.
 *
 * Si la lectura falla o no llega a tiempo todo se muestra: esconder el portal entero porque el CRM
 * no contesta sería peor que mostrar algo apagado un rato. Por eso no se reintenta.
 */
export function useFeatureSwitches() {
  const { hasRole } = useAuth()
  const query = useQuery({
    queryKey: featureSwitchKeys.todos,
    queryFn: () => withDeadline(api.listFeatureSwitches(), PLAZO_DE_LECTURA),
    select: storedSwitchesFrom,
    enabled: hasRole(ROLES.platformStaff),
    retry: false,
    staleTime: CINCO_MINUTOS,
    refetchInterval: CINCO_MINUTOS,
    refetchOnWindowFocus: true,
  })
  const stored = query.data ?? NO_STORED_SWITCHES
  return {
    stored,
    isEnabled: (key: SwitchKey) => isSwitchEnabled(stored, key),
    isLoading: query.isLoading,
    error: query.error,
  }
}

/**
 * Enciende o apaga una clave. La caché cambia al instante, a nombre de quien la cambió; si el
 * servicio la rechaza vuelve atrás solo esa clave, y al final se relee lo que quedó guardado.
 * Mientras una clave espera su respuesta, `isPending(key)` lo dice, para no mandarla dos veces.
 */
export function useSetFeatureSwitch() {
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const mutation = useMutation({
    mutationKey: setFeatureSwitchKey,
    mutationFn: ({ key, enabled }: SwitchVariables) => api.setFeatureSwitch(key, { enabled }),
    onMutate: async ({ key, enabled }) => {
      await queryClient.cancelQueries({ queryKey: featureSwitchKeys.todos })
      const previa = queryClient
        .getQueryData<FeatureSwitchResponse[]>(featureSwitchKeys.todos)
        ?.find((row) => row.key === key)
      const fila: FeatureSwitchResponse = {
        key,
        enabled,
        changedById: user?.sub ?? null,
        changedByName: user?.nombre ?? null,
        changedAt: new Date().toISOString(),
      }
      queryClient.setQueryData<FeatureSwitchResponse[]>(featureSwitchKeys.todos, (filas = []) =>
        replaceRow(filas, key, fila),
      )
      return { previa }
    },
    onError: (_error, { key }, context) => {
      queryClient.setQueryData<FeatureSwitchResponse[]>(
        featureSwitchKeys.todos,
        (filas) => filas && replaceRow(filas, key, context?.previa),
      )
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: featureSwitchKeys.todos }),
  })
  const pendientes = useMutationState({
    filters: { mutationKey: setFeatureSwitchKey, status: 'pending' },
    select: (m) => (m.state.variables as SwitchVariables).key,
  })
  return {
    set: (key: SwitchKey | EstiloClasicoKey, enabled: boolean) => mutation.mutate({ key, enabled }),
    isPending: (key: SwitchKey | EstiloClasicoKey) => pendientes.includes(key),
    error: mutation.error,
  }
}

/** Qué formatos clásicos (ruleta, nombres pasando) están encendidos; los dos nacen apagados. */
export function useEstilosClasicos(): { ruleta: boolean; nombres: boolean } {
  const { stored } = useFeatureSwitches()
  return {
    ruleta: estiloClasicoActivo(stored, ESTILOS_CLASICOS.ruleta),
    nombres: estiloClasicoActivo(stored, ESTILOS_CLASICOS.nombres),
  }
}

/**
 * Si el portal público está abierto: solo con el módulo de convocatorias encendido. Lo lee
 * cualquiera, con sesión o sin ella, y a diferencia de `useFeatureSwitches` falla cerrado: si no
 * se sabe, el portal no se muestra (una cartelera abierta por error expone eventos privados).
 */
export function usePublicPortalOpen(): { open: boolean; isLoading: boolean } {
  const query = useQuery({
    queryKey: ['feature-switches', 'public'],
    queryFn: () => withDeadline(api.listPublicFeatureSwitches(), PLAZO_DE_LECTURA),
    select: storedSwitchesFrom,
    retry: false,
    staleTime: CINCO_MINUTOS,
  })
  return {
    open: query.isSuccess && isSwitchEnabled(query.data, 'convocatorias'),
    isLoading: query.isLoading,
  }
}
