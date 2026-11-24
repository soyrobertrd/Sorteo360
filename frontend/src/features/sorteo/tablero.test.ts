import { describe, expect, it } from 'vitest'
import type { PrizeResponse } from '@/api/types'
import { barajar, gruposDePremios, numeroVisible, pendientes } from './tablero'

const premio = (id: string, name: string, value: string | null, drawn = false) =>
  ({ id, name, value, state: drawn ? 'drawn' : 'undrawn', draws: [] }) as unknown as PrizeResponse

describe('tablero', () => {
  it('agrupa premios iguales por ronda aunque no lleguen seguidos', () => {
    const grupos = gruposDePremios({
      rounds: [
        {
          id: 'r1',
          number: 1,
          prizes: [
            premio('a', 'Bono', 'RD$10,000.00', true),
            premio('b', 'Bono', 'RD$10,000.00'),
            premio('c', 'TV', null),
            premio('d', 'Bono', 'RD$10,000.00'),
          ],
        },
      ],
    } as never)
    expect(grupos.map((g) => [g.nombre, g.premios.length])).toEqual([
      ['Bono', 3],
      ['TV', 1],
    ])
    expect(pendientes(grupos[0]).map((p) => p.id)).toEqual(['b', 'd'])
  })

  it('muestra el número relleno', () => {
    expect(
      numeroVisible({ winnerEmployeeCode: '29370', winnerTicketNumber: '482-917' }, 'empleado', 7),
    ).toBe('0029370')
    expect(
      numeroVisible({ winnerEmployeeCode: null, winnerTicketNumber: '482-917' }, 'boleto', 7),
    ).toBe('0482917')
    expect(numeroVisible({}, 'boleto', 4)).toBe('----')
    // El ID del empleado con letras o guiones se muestra completo.
    expect(numeroVisible({ winnerEmployeeCode: 'emp-0124' }, 'empleado', 7)).toBe('EMP-0124')
    expect(numeroVisible({ winnerEmployeeCode: '001-1234567-8' }, 'empleado', 7)).toBe(
      '001-1234567-8',
    )
  })

  it('baraja un lote sin perder ni repetir filas', () => {
    const filas = Array.from({ length: 10 }, (_, i) => i)
    const barajadas = barajar(filas)
    expect([...barajadas].sort((a, b) => a - b)).toEqual(filas)
    expect(filas).toEqual(Array.from({ length: 10 }, (_, i) => i))
  })
})
