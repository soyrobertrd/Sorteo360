import { describe, expect, it } from 'vitest'
import type { CiudadResponse } from '@/api/types'
import { cambioDeCiudad } from './ciudadDelEvento'

const chicago: CiudadResponse = {
  id: 'c-1',
  name: 'Chicago',
  country: 'Estados Unidos',
  date: '2026-11-20',
  displayOrder: 1,
  timeZone: 'America/Chicago',
}

describe('cambioDeCiudad', () => {
  it('corregir la zona manda la ciudad entera, con su fecha y su orden tal como estaban', () => {
    expect(
      cambioDeCiudad(chicago, {
        pais: 'Estados Unidos',
        ciudad: 'Chicago',
        zona: 'America/Denver',
      }),
    ).toEqual({
      ciudadId: 'c-1',
      body: {
        name: 'Chicago',
        country: 'Estados Unidos',
        date: '2026-11-20',
        displayOrder: 1,
        timeZone: 'America/Denver',
      },
    })
  })

  it('no manda nada cuando país, ciudad y zona siguen como estaban', () => {
    expect(
      cambioDeCiudad(chicago, {
        pais: 'Estados Unidos',
        ciudad: 'Chicago',
        zona: 'America/Chicago',
      }),
    ).toBeUndefined()
  })
})
