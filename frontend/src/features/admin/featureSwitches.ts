import type { LucideIcon } from 'lucide-react'
import type { FeatureSwitchResponse } from '@/api/types'
import { formatDayMonth } from '@/lib/format'
import { moduleOf, type CatalogueModule, type SwitchKey } from './featureCatalogue'
import { menuOf } from './sidebarMenus'

/**
 * Las filas guardadas, por clave. Solo llegan las que alguien cambió o sembró una migración: una
 * clave sin fila está encendida.
 */
export type StoredSwitches = ReadonlyMap<string, FeatureSwitchResponse>

/**
 * Los formatos clásicos de la pantalla del sorteo. Al revés que el resto, nacen apagados: el
 * sorteo usa su tablero de ganadores y Administración los enciende con un check. Solo una fila
 * guardada en `true` los enciende; sin lectura o sin fila, apagados.
 */
export const ESTILOS_CLASICOS = {
  ruleta: 'sorteo.style-roulette',
  nombres: 'sorteo.style-names',
} as const

export type EstiloClasicoKey = (typeof ESTILOS_CLASICOS)[keyof typeof ESTILOS_CLASICOS]

export const estiloClasicoActivo = (stored: StoredSwitches, key: EstiloClasicoKey): boolean =>
  stored.get(key)?.enabled === true

/** Sin filas: lo que se usa mientras no hay lectura, o cuando falló. Todo encendido. */
export const NO_STORED_SWITCHES: StoredSwitches = new Map()

export const storedSwitchesFrom = (rows: readonly FeatureSwitchResponse[]): StoredSwitches =>
  new Map(rows.map((row) => [row.key, row]))

/** Si la clave tiene su propia fila apagada, sin mirar su módulo. */
export const isOwnSwitchOff = (stored: StoredSwitches, key: string): boolean =>
  stored.get(key)?.enabled === false

/**
 * Si una clave se muestra. Una función se esconde cuando ella o su módulo tienen una fila apagada:
 * apagar un módulo esconde todas sus funciones sin tocar sus interruptores.
 */
export const isSwitchEnabled = (stored: StoredSwitches, key: SwitchKey): boolean =>
  !isOwnSwitchOff(stored, moduleOf(key)) && !isOwnSwitchOff(stored, key)

/**
 * Las filas con la de `key` cambiada por `row`, o quitada si `row` no viene. Sirve para escribir
 * un cambio antes de que el servicio conteste y para deshacer solo ese, sin tocar los demás.
 */
export function replaceRow(
  rows: readonly FeatureSwitchResponse[],
  key: string,
  row: FeatureSwitchResponse | undefined,
): FeatureSwitchResponse[] {
  return [...rows.filter((r) => r.key !== key), ...(row ? [row] : [])]
}

/** Rechaza si la promesa no termina en `ms`. La petición sigue su curso; solo se deja de esperar. */
export function withDeadline<T>(promise: Promise<T>, ms: number): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined
  const deadline = new Promise<never>((_, reject) => {
    timer = setTimeout(() => reject(new Error(`Sin respuesta en ${ms} ms.`)), ms)
  })
  return Promise.race([promise, deadline]).finally(() => clearTimeout(timer))
}

/**
 * Cuántas funciones del módulo tienen su propio interruptor apagado. Apagar el módulo no las
 * cuenta: sus interruptores se conservan y vuelven tal cual al encenderlo.
 */
export function disabledFeatureCount(stored: StoredSwitches, module: CatalogueModule): number {
  return module.features.filter((feature) => isOwnSwitchOff(stored, feature.key)).length
}

export interface PreviewItem {
  label: string
  icon: LucideIcon
  /** Solo la ve quien tiene el rol; la vista previa es la de un usuario con todos. */
  roleGated: boolean
}

/**
 * La barra lateral del módulo como la vería un usuario con todos los roles: la misma que dibuja
 * `AppShell`, con el Dashboard, que no se apaga, y las entradas encendidas. Un módulo apagado no
 * tiene barra.
 */
export function sidebarPreview(stored: StoredSwitches, module: CatalogueModule): PreviewItem[] {
  if (!isSwitchEnabled(stored, module.key as SwitchKey)) return []
  const menu = menuOf(module)
  return [
    menu.dashboard,
    ...menu.entries.filter((entry) => !entry.key || isSwitchEnabled(stored, entry.key)),
  ].map((entry) => ({ label: entry.label, icon: entry.icon, roleGated: entry.role !== undefined }))
}

/**
 * El último cambio de un interruptor: "Desactivada por María Fernández, 2 nov". Nada si no tiene
 * fila o si la sembró una migración, porque nadie lo cambió.
 */
export function lastChangeLabel(row: FeatureSwitchResponse | undefined): string | null {
  if (!row?.changedAt) return null
  const fecha = formatDayMonth(row.changedAt)
  const accion = row.enabled ? 'Activada' : 'Desactivada'
  return row.changedByName ? `${accion} por ${row.changedByName}, ${fecha}` : `${accion}, ${fecha}`
}
