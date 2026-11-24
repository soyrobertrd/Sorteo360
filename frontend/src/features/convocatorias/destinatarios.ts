import { useQuery } from '@tanstack/react-query'
import { api } from '@/api'
import type { ListContactsParams } from '@/api/types'

/**
 * A quién se le puede mandar una campaña. Es lo único que queda del CRM en el frontend: el
 * asistente de campañas todavía elige contactos, listas y clasificaciones de ahí.
 * shortcut: el módulo de convocatorias está oculto; cuando se reactive, las campañas apuntan al
 * padrón de participantes del evento y este archivo se borra (plans/sorteo360.md, Fase 4).
 */

/** Los códigos de clasificación por la etiqueta con que se guardan en una lista vieja. */
const CODIGO_POR_ETIQUETA: Record<string, string> = {
  Aliado: 'Ally',
  'Agencia de viajes': 'TravelAgency',
  'Tour operador': 'TourOperator',
  Aerolínea: 'Airline',
  Prensa: 'Press',
}

/** El código de clasificación de una lista guardada, venga como etiqueta o como código. */
export function codigoDeClasificacion(tab: string): string {
  return CODIGO_POR_ETIQUETA[tab] ?? tab
}

/** El catálogo de clasificaciones de contacto: se pide una vez por sesión. */
export function useContactClassifications() {
  return useQuery({
    queryKey: ['contact-classifications'],
    queryFn: () => api.listContactClassifications(),
    staleTime: 5 * 60 * 1000,
  })
}

export function useContacts(params: ListContactsParams, enabled = true) {
  return useQuery({
    queryKey: ['contactos', 'list', params],
    queryFn: () => api.listContacts(params),
    enabled,
  })
}

export function useContactLists() {
  return useQuery({
    queryKey: ['contactos', 'contact-lists'],
    queryFn: () => api.listContactLists(),
  })
}

export function useContactList(id: string | null) {
  return useQuery({
    queryKey: ['contactos', 'contact-lists', id ?? ''],
    queryFn: () => api.getContactList(id ?? ''),
    enabled: Boolean(id),
  })
}
