import { describe, expect, it } from 'vitest'
import { ApiError } from '../client'
import type { CreateEventoRequest, MontajeEvento, UpdateEventoRequest } from '../types'
import { MockApiClient } from './mockClient'
import { toUpdateRaffleRequest } from '@/features/sorteo/raffleRequest'

// ev-0002: un evento sembrado en planificación, así que admite corregir su ciudad.
const EVENTO_SEMBRADO = 'ev-0002'

function ciudad(timeZone: string) {
  return { name: 'Chicago', country: 'Estados Unidos', date: null, timeZone }
}

async function ciudadDelEvento(api: MockApiClient): Promise<string> {
  const { cities } = await api.getEvento(EVENTO_SEMBRADO)
  return cities[0].id
}

describe('MockApiClient, zona horaria de la ciudad', () => {
  it('guarda la zona corregida y la devuelve en el evento', async () => {
    const api = new MockApiClient()
    const id = await ciudadDelEvento(api)
    const editada = await api.updateCiudad(EVENTO_SEMBRADO, id, ciudad('America/Chicago'))
    expect(editada.timeZone).toBe('America/Chicago')
    const evento = await api.getEvento(EVENTO_SEMBRADO)
    expect(evento.cities.find((c) => c.id === id)?.timeZone).toBe('America/Chicago')
  })

  it.each(['America/Gotham', 'Central Standard Time', ''])(
    'rechaza %j con TIME_ZONE_UNKNOWN al editar',
    async (zona) => {
      const api = new MockApiClient()
      const error = await api
        .updateCiudad(EVENTO_SEMBRADO, await ciudadDelEvento(api), ciudad(zona))
        .catch((e: unknown) => e)
      expect(error).toBeInstanceOf(ApiError)
      expect(error).toMatchObject({ status: 400, code: 'TIME_ZONE_UNKNOWN' })
    },
  )
})

describe('MockApiClient, montaje del salón del evento', () => {
  function alta(seatingLayout?: string | null): CreateEventoRequest {
    return {
      type: 'Conference',
      name: 'Conferencia de prueba',
      startDate: '2026-12-01',
      endDate: '2026-12-01',
      language: 'es',
      city: 'Madrid',
      country: 'España',
      timeZone: 'Europe/Madrid',
      ...(seatingLayout === undefined ? {} : { seatingLayout: seatingLayout as MontajeEvento }),
    }
  }

  it('crea un evento sin montaje y lo devuelve nulo', async () => {
    const api = new MockApiClient()
    const id = await api.createEvento(alta())
    expect((await api.getEvento(id)).seatingLayout).toBeNull()
  })

  it('acepta el montaje sin importar mayúsculas y lo devuelve como el servicio', async () => {
    const api = new MockApiClient()
    const id = await api.createEvento(alta('uSHAPE'))
    expect((await api.getEvento(id)).seatingLayout).toBe('UShape')
  })

  it.each(['Theater', ''])(
    'rechaza %j con un 400 sobre SeatingLayout, sin código',
    async (valor) => {
      const error = await new MockApiClient().createEvento(alta(valor)).catch((e: unknown) => e)
      expect(error).toBeInstanceOf(ApiError)
      expect(error).toMatchObject({ status: 400, code: undefined })
      expect(Object.keys((error as ApiError).fieldErrors ?? {})).toEqual(['SeatingLayout'])
    },
  )
})

describe('MockApiClient, el PATCH del evento reemplaza lo editable (ADR-0009)', () => {
  /** Lo que una pantalla manda al editar el evento sembrado, con valor en lo que se puede borrar. */
  async function edicionCompleta(api: MockApiClient): Promise<UpdateEventoRequest> {
    const e = await api.getEvento(EVENTO_SEMBRADO)
    return {
      name: e.name,
      startDate: e.startDate,
      endDate: e.endDate,
      language: e.language ?? 'es',
      visibility: e.visibility,
      externalUrl: null,
      invitedCount: e.invitedCount ?? null,
      estimatedBudget: { amount: 5000, currency: 'USD' },
      description: 'Cena con operadores',
      seatingLayout: 'Conference',
    }
  }

  // Abrir la convocatoria omitía la descripción y el presupuesto, y el servicio los borraba;
  // el mock los conservaba y lo escondía.
  it.each(['seatingLayout', 'description', 'estimatedBudget'] as const)(
    'borra %s cuando el PATCH lo omite',
    async (campo) => {
      const api = new MockApiClient()
      const completa = await edicionCompleta(api)
      await api.updateEvento(EVENTO_SEMBRADO, completa)
      expect((await api.getEvento(EVENTO_SEMBRADO))[campo]).toEqual(completa[campo])

      const sinCampo: Partial<UpdateEventoRequest> = { ...completa }
      delete sinCampo[campo]
      await api.updateEvento(EVENTO_SEMBRADO, sinCampo as UpdateEventoRequest)
      expect((await api.getEvento(EVENTO_SEMBRADO))[campo]).toBeNull()
    },
  )
})

describe('MockApiClient, sorteos vinculados', () => {
  async function sorteoNuevo(api: MockApiClient, name: string): Promise<string> {
    const id = await api.createEvento({
      type: 'Corporate',
      name,
      startDate: '2026-12-01',
      endDate: '2026-12-01',
      language: 'es',
      city: 'Santo Domingo',
      country: 'República Dominicana',
      timeZone: 'America/Santo_Domingo',
    })
    await api.createRaffle(id)
    return id
  }

  async function vincular(api: MockApiClient, eventId: string, linkedEventId: string | null) {
    const raffle = await api.getRaffle(eventId)
    await api.updateRaffle(eventId, { ...toUpdateRaffleRequest(raffle, {}), linkedEventId })
  }

  it('vincularse a un sorteo vinculado lleva al evento de su grupo, y el ancla no se vincula', async () => {
    const api = new MockApiClient()
    const segundo = await sorteoNuevo(api, 'Segundo sorteo')
    const tercero = await sorteoNuevo(api, 'Tercer sorteo')
    await vincular(api, segundo, 'ev-0001')

    await vincular(api, tercero, segundo)

    expect((await api.getRaffle(tercero)).linkedEventId).toBe('ev-0001')
    // ev-0001 es el ancla de los otros dos: no se vincula a un sorteo de otro grupo.
    const aparte = await sorteoNuevo(api, 'Sorteo aparte')
    await expect(vincular(api, 'ev-0001', aparte)).rejects.toMatchObject({
      code: 'RAFFLE_LINK_INVALID',
    })
    await expect(vincular(api, segundo, segundo)).rejects.toMatchObject({
      code: 'RAFFLE_LINK_INVALID',
    })
  })
})
