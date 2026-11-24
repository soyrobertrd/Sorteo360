import { describe, expect, it } from 'vitest'
import {
  CAMPOS_VISIBLES_POR_DEFECTO,
  camposDe,
  detalleVisible,
  ganadorVisible,
  identificadorVisible,
  nombreVisible,
} from './camposVisibles'

const ana = {
  winnerName: 'Ana María Pérez Gómez',
  winnerFirstName: 'Ana María',
  winnerLastName: 'Pérez Gómez',
  winnerCompany: 'Finanzas',
  winnerTicketNumber: '482-917',
  winnerEmployeeCode: 'BR-0029370',
  winnerDepartment: 'Tesorería',
  winnerLocation: 'Santiago',
}

describe('camposVisibles', () => {
  it('arma el nombre según el modo', () => {
    expect(nombreVisible(ana, 'full_name')).toBe('Ana María Pérez Gómez')
    expect(nombreVisible(ana, 'first_and_last')).toBe('Ana Pérez')
    expect(nombreVisible(ana, 'first_name')).toBe('Ana María')
  })

  it('sin nombre y apellido separados cae al nombre completo', () => {
    const luis = { ...ana, winnerName: 'Luis Gómez', winnerFirstName: null, winnerLastName: null }
    expect(nombreVisible(luis, 'first_and_last')).toBe('Luis Gómez')
    expect(nombreVisible(luis, 'first_name')).toBe('Luis Gómez')
  })

  it('el identificador y el detalle vacíos no se muestran', () => {
    expect(identificadorVisible(ana, 'employee_code')).toBe('BR-0029370')
    expect(identificadorVisible(ana, 'none')).toBeNull()
    expect(identificadorVisible({ ...ana, winnerTicketNumber: '  ' }, 'ticket_number')).toBeNull()
    expect(detalleVisible(ana, 'location')).toBe('Santiago')
    // Sin ubicación cae al departamento, para no dejar la línea en blanco.
    expect(detalleVisible({ ...ana, winnerLocation: null }, 'location')).toBe('Tesorería')
    expect(detalleVisible({ ...ana, winnerDepartment: null }, 'department')).toBe('Finanzas')
    expect(detalleVisible(ana, 'none')).toBeNull()
  })

  it('sin campos guardados usa nombre completo, boleto y ubicación', () => {
    expect(camposDe({})).toEqual(CAMPOS_VISIBLES_POR_DEFECTO)
    expect(ganadorVisible(ana, camposDe(undefined))).toEqual({
      nombre: 'Ana María Pérez Gómez',
      identificador: '482-917',
      detalle: 'Santiago',
    })
  })
})
