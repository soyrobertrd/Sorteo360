import { expect, it } from 'vitest'
import type { RaffleResponse } from '@/api/types'
import { bitacoraCsv, bitacoraDe } from './bitacora'

const raffle = {
  rounds: [
    {
      number: 1,
      name: 'Navidad',
      prizes: [
        {
          name: 'TV',
          draws: [
            {
              winnerName: 'Ana',
              drawnAt: '2026-12-18T20:00:00Z',
              drawnByName: 'Operadora',
              eligiblePoolSize: 12,
              void: { code: 'absent', note: 'No vino', voidedAt: '2026-12-18T20:01:00Z' },
              handovers: [],
            },
            {
              winnerName: 'Luis',
              drawnAt: '2026-12-18T20:02:00Z',
              eligiblePoolSize: 11,
              void: null,
              handovers: [
                {
                  receiverName: 'Luis',
                  redeemedAt: '2026-12-18T21:00:00Z',
                  redeemedByName: 'Puerta',
                  reversedAt: null,
                },
              ],
            },
          ],
        },
      ],
    },
  ],
} as unknown as RaffleResponse

it('ordena de lo más reciente a lo más viejo, con quién lo hizo', () => {
  const b = bitacoraDe(raffle)
  expect(b.map((e) => [e.accion, e.participante])).toEqual([
    ['entrega', 'Luis'],
    ['sorteo', 'Luis'],
    ['anulacion', 'Ana'],
    ['sorteo', 'Ana'],
  ])
  expect(b[3].usuario).toBe('Operadora')
  expect(b[1].usuario).toBeNull()
})

it('exporta CSV con encabezado y comillas escapadas', () => {
  const csv = bitacoraCsv(bitacoraDe(raffle))
  expect(csv.split('\r\n')[0]).toBe('Fecha;Acción;Ronda;Premio;Participante;Usuario;Detalle')
  expect(csv).toContain('"Anulación";"Ronda 1: Navidad";"TV";"Ana";"";"No vino"')
})
