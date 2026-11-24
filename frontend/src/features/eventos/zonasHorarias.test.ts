import { describe, expect, it } from 'vitest'
import { detalleDeZonaHoraria, esZonaHorariaConocida, zonaHorariaDeCiudad } from './zonasHorarias'

describe('zonaHorariaDeCiudad', () => {
  it('da la zona de una ciudad de la lista', () => {
    expect(zonaHorariaDeCiudad('Estados Unidos', 'Chicago')).toBe('America/Chicago')
    expect(zonaHorariaDeCiudad('España', 'Madrid')).toBe('Europe/Madrid')
  })

  it('reconoce la ciudad escrita a mano sin mayúsculas ni tildes', () => {
    expect(zonaHorariaDeCiudad('  estados unidos', 'NUEVA YORK ')).toBe('America/New_York')
    expect(zonaHorariaDeCiudad('Canada', 'montreal')).toBe('America/Toronto')
  })

  it('cae en Santo Domingo cuando la ciudad no está en la lista', () => {
    expect(zonaHorariaDeCiudad('Japón', 'Tokio')).toBe('America/Santo_Domingo')
    expect(zonaHorariaDeCiudad('Otro', 'Otro')).toBe('America/Santo_Domingo')
  })
})

describe('detalleDeZonaHoraria', () => {
  it('da la diferencia con UTC del día pedido, que cambia con el horario de verano', () => {
    expect(detalleDeZonaHoraria('America/Chicago', new Date('2026-01-15T12:00:00Z'))).toBe(
      'UTC−06:00 (hora central)',
    )
    expect(detalleDeZonaHoraria('America/Chicago', new Date('2026-07-15T12:00:00Z'))).toBe(
      'UTC−05:00 (hora central)',
    )
  })

  it('escribe las diferencias positivas y las de media hora', () => {
    expect(detalleDeZonaHoraria('Asia/Kolkata', new Date('2026-07-15T12:00:00Z'))).toMatch(
      /^UTC\+05:30 \(.+\)$/,
    )
  })
})

describe('esZonaHorariaConocida', () => {
  it('acepta un id de la lista del navegador y rechaza uno mal escrito', () => {
    expect(esZonaHorariaConocida('America/Chicago')).toBe(true)
    expect(esZonaHorariaConocida('America/Chicgo')).toBe(false)
    expect(esZonaHorariaConocida('')).toBe(false)
  })

  it('acepta la zona que la ciudad ya tiene guardada aunque la lista no la traiga', () => {
    expect(esZonaHorariaConocida('UTC', 'UTC')).toBe(true)
  })
})
