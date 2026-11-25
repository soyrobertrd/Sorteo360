import { describe, expect, it } from 'vitest'
import type { CreateEventoRequest } from '../types'
import { MockApiClient } from './mockClient'

const ALTA: CreateEventoRequest = {
  type: 'Conference',
  name: 'Fiesta A',
  startDate: '2026-12-01',
  endDate: '2026-12-01',
  language: 'es',
  city: 'Santo Domingo',
  country: 'República Dominicana',
  timeZone: 'America/Santo_Domingo',
}

/** Un evento con sorteo en borrador y estas personas en el padrón, todas presentes. */
async function sorteoCon(
  api: MockApiClient,
  nombre: string,
  personas: { fullName: string; email?: string; employeeCode?: string; checkedIn?: boolean }[],
): Promise<string> {
  const eventId = await api.createEvento({ ...ALTA, name: nombre })
  await api.createRaffle(eventId)
  await api.importRaffleParticipants(eventId, {
    participants: personas.map((p) => ({ checkedIn: true, ...p })),
    replaceExisting: true,
    source: 'excel',
  })
  return eventId
}

describe('MockApiClient, exclusión en lote', () => {
  it('excluye a los ganadores vivos de otro sorteo, también a quien no tiene correo, por su ID', async () => {
    const api = new MockApiClient()
    const pedro = { fullName: 'Pedro Sin Correo', employeeCode: 'E-55' }
    const ana = { fullName: 'Ana Uno', email: 'ana@banco.com', employeeCode: 'E-1' }
    const a = await sorteoCon(api, 'Fiesta A', [pedro])
    const ronda = await api.createRaffleRound(a, { name: 'Ronda', description: null })
    const [premio] = await api.createPrizes(a, {
      roundId: ronda,
      name: 'Bono',
      description: null,
      value: null,
      quantity: 1,
    })
    await api.startRaffle(a)
    await api.drawPrize(a, premio.id)
    const b = await sorteoCon(api, 'Fiesta B', [pedro, ana])

    const res = await api.bulkExcludeAttendees(b, {
      sourceEventId: a,
      identifiers: null,
      note: null,
    })

    expect(res).toEqual({
      excluded: 1,
      alreadyExcluded: 0,
      notInRoster: [],
      invalid: [],
      unidentifiable: [],
    })
    const padron = await api.listRaffleParticipants(b)
    expect(padron.find((p) => p.employeeCode === 'E-55')).toMatchObject({
      email: null,
      identityKey: 'id:e-55',
      override: 'excluded',
    })
  }, 30_000)

  it('informa a cada uno en su grupo y no repite a quien ya estaba excluido', async () => {
    const api = new MockApiClient()
    const b = await sorteoCon(api, 'Fiesta B', [
      { fullName: 'Ana Uno', email: 'ana@banco.com' },
      { fullName: 'Luis Ausente', email: 'luis@banco.com', checkedIn: false },
      { fullName: 'Pedro Sin Correo', employeeCode: 'E-55' },
    ])
    const lista = ['ANA@banco.com ', 'e-55', 'luis@banco.com', 'x@y.com', 'E-999', 'no@es', '']

    const primera = await api.bulkExcludeAttendees(b, {
      sourceEventId: null,
      identifiers: lista,
      note: null,
    })
    const segunda = await api.bulkExcludeAttendees(b, {
      sourceEventId: null,
      identifiers: lista,
      note: null,
    })

    expect(primera).toEqual({
      excluded: 2,
      alreadyExcluded: 0,
      notInRoster: ['luis@banco.com', 'x@y.com'],
      invalid: ['no@es', 'E-999'],
      unidentifiable: [],
    })
    expect(segunda.excluded).toBe(0)
    expect(segunda.alreadyExcluded).toBe(2)
  }, 30_000)

  it('rechaza con los mismos códigos que el servicio', async () => {
    const api = new MockApiClient()
    const b = await sorteoCon(api, 'Fiesta B', [{ fullName: 'Ana Uno', email: 'ana@banco.com' }])

    await expect(
      api.bulkExcludeAttendees(b, { sourceEventId: null, identifiers: null, note: null }),
    ).rejects.toMatchObject({ status: 400, code: 'RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED' })
    await expect(
      api.bulkExcludeAttendees(b, { sourceEventId: b, identifiers: null, note: null }),
    ).rejects.toMatchObject({ status: 400, code: 'RAFFLE_BULK_EXCLUDE_SAME_EVENT' })

    const ronda = await api.createRaffleRound(b, { name: 'Ronda', description: null })
    await api.createPrizes(b, {
      roundId: ronda,
      name: 'Bono',
      description: null,
      value: null,
      quantity: 1,
    })
    await api.startRaffle(b)
    await expect(
      api.bulkExcludeAttendees(b, {
        sourceEventId: null,
        identifiers: ['ana@banco.com'],
        note: null,
      }),
    ).rejects.toMatchObject({ status: 409, code: 'RAFFLE_NOT_DRAFT' })
  }, 30_000)
})
