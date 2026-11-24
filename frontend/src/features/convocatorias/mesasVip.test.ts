import { describe, expect, it } from 'vitest'
import { CONFIG_POR_DEFECTO, esReservada, maxSillasPorMesa, MESA_PRINCIPAL } from './mesas'

describe('mesas VIP', () => {
  const config = { ...CONFIG_POR_DEFECTO, modo: 'manual' as const, mesas: 3, reservadas: 1 }

  it('sin elección, son VIP las primeras reservadas', () => {
    expect([1, 2, 3].map((m) => esReservada(config, m))).toEqual([true, false, false])
  })

  it('con elección, son VIP las elegidas', () => {
    expect([1, 2, 3].map((m) => esReservada({ ...config, vip: [3] }, m))).toEqual([
      false,
      false,
      true,
    ])
  })

  it('la mesa principal siempre es de protocolo', () => {
    expect(esReservada({ ...config, vip: [] }, MESA_PRINCIPAL)).toBe(true)
  })

  it('redonda hasta 12 sillas, taller hasta 6', () => {
    expect(maxSillasPorMesa('mesas')).toBe(12)
    expect(maxSillasPorMesa('escuela')).toBe(6)
  })
})
