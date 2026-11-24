import { describe, expect, it } from 'vitest'
import { isWellFormedSwitchKey } from '@/lib/validation/switchKey'
import { FEATURE_CATALOGUE, moduleForPath } from './featureCatalogue'
import { ADMIN_MENU, menuForPath } from './sidebarMenus'

const CLAVES = FEATURE_CATALOGUE.flatMap((module) => [
  module.key,
  ...module.features.map((feature) => feature.key),
])

describe('el catálogo de interruptores', () => {
  it('no repite ninguna clave', () => {
    expect(new Set(CLAVES).size).toBe(CLAVES.length)
  })

  it.each(CLAVES)('%s tiene la forma aceptada por el backend', (key) => {
    expect(isWellFormedSwitchKey(key)).toBe(true)
  })

  it('cada función lleva la clave de su módulo delante', () => {
    for (const module of FEATURE_CATALOGUE) {
      for (const feature of module.features) {
        expect(feature.key.startsWith(`${module.key}.`)).toBe(true)
      }
    }
  })

  it('los módulos son los tres configurables en la plataforma', () => {
    expect(FEATURE_CATALOGUE.map((m) => m.key)).toEqual(['sorteo', 'events', 'convocatorias'])
  })
})

describe('moduleForPath', () => {
  it.each([
    ['/convocatorias/eventos/1/campanas/2', 'convocatorias'],
    ['/sorteo/historial', 'sorteo'],
    ['/eventos/todos', 'events'],
  ])('%s es de %s', (ruta, modulo) => {
    expect(moduleForPath(ruta).key).toBe(modulo)
  })

  it('lo que no es de ningún módulo cae en Sorteo', () => {
    expect(moduleForPath('/configuracion').key).toBe('sorteo')
  })

  it('un prefijo solo cuenta entero: /eventosx no es Eventos', () => {
    expect(moduleForPath('/eventosx').key).toBe('sorteo')
  })
})

describe('menuForPath', () => {
  it('Administración tiene su propio menú', () => {
    expect(menuForPath('/admin')).toBe(ADMIN_MENU)
    expect(menuForPath('/admin/feature-switches')).toBe(ADMIN_MENU)
  })

  it('la barra de un módulo es su Dashboard y sus funciones, con el título del módulo', () => {
    const menu = menuForPath('/sorteo/sorteos')
    expect(menu.heading).toBe('Rifas en vivo')
    expect(menu.dashboard.to).toBe('/sorteo')
    expect(menu.entries.map((e) => e.to)).toEqual(['/sorteo/sorteos', '/sorteo/historial'])
  })
})
