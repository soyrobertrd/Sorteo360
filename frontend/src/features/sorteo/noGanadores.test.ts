import { describe, expect, it } from 'vitest'
import type { PrizeDrawSummary, RaffleParticipantItem } from '@/api/types'
import { noGanadores } from './noGanadores'

const persona = (n: number, extra: Partial<RaffleParticipantItem> = {}): RaffleParticipantItem => ({
  id: `p${n}`,
  ticketNumber: `T-${n}`,
  fullName: `Persona ${n}`,
  email: null,
  identityKey: `id:e${n}`,
  company: null,
  employeeCode: `E${n}`,
  qrCode: `QR-${n}`,
  checkedIn: true,
  checkedInAt: null,
  source: 'excel',
  qualified: true,
  override: null,
  ...extra,
})

const draw = (extra: Partial<PrizeDrawSummary>): PrizeDrawSummary =>
  ({ id: 'd', winnerAttendeeId: 'a', winnerName: 'x', void: null, ...extra }) as PrizeDrawSummary

describe('noGanadores', () => {
  it('saca al ganador vivo por su boleto y deja pasar al anulado', () => {
    const r = noGanadores(
      [persona(1), persona(2), persona(3)],
      [
        draw({ winnerTicketNumber: 't-1' }),
        draw({ winnerTicketNumber: 'T-2', void: { code: 'absent' } as PrizeDrawSummary['void'] }),
      ],
    )
    expect(r.map((p) => p.employeeCode)).toEqual(['E2', 'E3'])
  })

  it('sin boleto, reconoce al ganador por su ID de empleado', () => {
    const r = noGanadores(
      [persona(1, { ticketNumber: null }), persona(2)],
      [draw({ winnerTicketNumber: null, winnerEmployeeCode: ' e1 ' })],
    )
    expect(r.map((p) => p.employeeCode)).toEqual(['E2'])
  })
})
