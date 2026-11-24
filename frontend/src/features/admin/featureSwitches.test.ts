import { afterEach, describe, expect, it, vi } from 'vitest'
import type { FeatureSwitchResponse } from '@/api/types'
import { CATALOGUE_BY_KEY } from './featureCatalogue'
import {
  disabledFeatureCount,
  isSwitchEnabled,
  lastChangeLabel,
  NO_STORED_SWITCHES,
  replaceRow,
  sidebarPreview,
  storedSwitchesFrom,
  withDeadline,
} from './featureSwitches'

const fila = (key: string, enabled: boolean, cambio = true): FeatureSwitchResponse => ({
  key,
  enabled,
  changedById: cambio ? 'u-admin' : null,
  changedByName: cambio ? 'María Fernández' : null,
  changedAt: cambio ? '2026-11-02T15:00:00Z' : null,
})

const guardadas = (...filas: FeatureSwitchResponse[]) => storedSwitchesFrom(filas)
const convocatorias = CATALOGUE_BY_KEY.convocatorias

describe('isSwitchEnabled', () => {
  it('una clave sin fila está encendida', () => {
    expect(isSwitchEnabled(guardadas(), 'convocatorias.campaigns')).toBe(true)
  })

  it('una función apagada se esconde', () => {
    expect(
      isSwitchEnabled(guardadas(fila('convocatorias.campaigns', false)), 'convocatorias.campaigns'),
    ).toBe(false)
  })

  it('una función encendida en un módulo apagado se esconde', () => {
    const filas = guardadas(fila('convocatorias', false), fila('convocatorias.campaigns', true))
    expect(isSwitchEnabled(filas, 'convocatorias.campaigns')).toBe(false)
  })

  it('apagar una función no toca a sus hermanas', () => {
    expect(
      isSwitchEnabled(guardadas(fila('convocatorias.campaigns', false)), 'convocatorias.guests'),
    ).toBe(true)
  })

  it('un módulo apagado se esconde', () => {
    expect(isSwitchEnabled(guardadas(fila('sorteo', false)), 'sorteo')).toBe(false)
  })

  it('sin lectura, todo se muestra', () => {
    expect(isSwitchEnabled(NO_STORED_SWITCHES, 'convocatorias.campaigns')).toBe(true)
  })
})

describe('replaceRow', () => {
  const antes = [fila('convocatorias.campaigns', false), fila('convocatorias.guests', false)]

  it('cambia solo la fila de su clave', () => {
    const despues = replaceRow(
      antes,
      'convocatorias.campaigns',
      fila('convocatorias.campaigns', true),
    )
    expect(despues.map((r) => [r.key, r.enabled])).toEqual([
      ['convocatorias.guests', false],
      ['convocatorias.campaigns', true],
    ])
  })

  it('deshacer un cambio que falló no pisa otro que llegó mientras tanto', () => {
    let filas = replaceRow(antes, 'convocatorias.campaigns', fila('convocatorias.campaigns', true))
    filas = replaceRow(filas, 'convocatorias.guests', fila('convocatorias.guests', true))
    filas = replaceRow(filas, 'convocatorias.campaigns', antes[0])
    const estado = storedSwitchesFrom(filas)
    expect(estado.get('convocatorias.campaigns')?.enabled).toBe(false)
    expect(estado.get('convocatorias.guests')?.enabled).toBe(true)
  })

  it('deshacer una clave que no tenía fila la quita', () => {
    const filas = replaceRow(antes, 'convocatorias.check-in', fila('convocatorias.check-in', false))
    expect(replaceRow(filas, 'convocatorias.check-in', undefined)).toEqual(antes)
  })
})

describe('withDeadline', () => {
  afterEach(() => vi.useRealTimers())

  it('deja pasar la respuesta que llega a tiempo', async () => {
    await expect(withDeadline(Promise.resolve([]), 3_000)).resolves.toEqual([])
  })

  it('rechaza cuando el plazo vence antes que la respuesta', async () => {
    vi.useFakeTimers()
    const colgada = new Promise<never>(() => {})
    const espera = expect(withDeadline(colgada, 3_000)).rejects.toThrow('Sin respuesta en 3000 ms.')
    await vi.advanceTimersByTimeAsync(3_000)
    await espera
  })
})

describe('disabledFeatureCount', () => {
  it('cuenta las funciones apagadas del módulo, no las de otro', () => {
    const filas = guardadas(
      fila('convocatorias.campaigns', false),
      fila('convocatorias.guests', false),
      fila('events.events', false),
    )
    expect(disabledFeatureCount(filas, convocatorias)).toBe(2)
  })

  it('una función vuelta a encender no cuenta', () => {
    expect(
      disabledFeatureCount(guardadas(fila('convocatorias.campaigns', true)), convocatorias),
    ).toBe(0)
  })

  it('apagar el módulo no cambia la cuenta de sus funciones', () => {
    expect(disabledFeatureCount(guardadas(fila('convocatorias', false)), convocatorias)).toBe(0)
  })
})

describe('sidebarPreview', () => {
  it('muestra el Dashboard y las funciones encendidas, en el orden de la barra', () => {
    const labels = sidebarPreview(
      guardadas(fila('convocatorias.campaigns', false)),
      convocatorias,
    ).map((i) => i.label)
    expect(labels).toEqual(['Dashboard', 'Eventos', 'Plantillas', 'Invitados', 'Check-in'])
  })

  it('un módulo apagado no tiene barra', () => {
    expect(sidebarPreview(guardadas(fila('convocatorias', false)), convocatorias)).toEqual([])
  })
})

describe('lastChangeLabel', () => {
  it('dice quién apagó y cuándo', () => {
    expect(lastChangeLabel(fila('convocatorias.campaigns', false))).toBe(
      'Desactivada por María Fernández, 2 nov',
    )
  })

  it('dice quién encendió y cuándo', () => {
    expect(lastChangeLabel(fila('convocatorias.campaigns', true))).toBe(
      'Activada por María Fernández, 2 nov',
    )
  })

  it('no dice nada sin fila ni en una fila sembrada', () => {
    expect(lastChangeLabel(undefined)).toBeNull()
    expect(lastChangeLabel(fila('convocatorias.campaigns', false, false))).toBeNull()
  })
})
