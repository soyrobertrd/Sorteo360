import { describe, expect, it } from 'vitest'
import {
  firmantesDelCertificado,
  lineaEnBlanco,
  toSetCertificateSignersRequest,
} from './certificateSigners'

const SIN_FIRMANTE = { nombre: '', cargo: '' }

describe('toSetCertificateSignersRequest', () => {
  it('manda los dos firmantes recortados y en orden', () => {
    expect(
      toSetCertificateSignersRequest([
        { nombre: '  Ana Pérez ', cargo: 'Ministra de Turismo  ' },
        { nombre: 'Luis Gómez', cargo: ' Director de Recursos Humanos' },
      ]),
    ).toEqual({
      signers: [
        { name: 'Ana Pérez', title: 'Ministra de Turismo' },
        { name: 'Luis Gómez', title: 'Director de Recursos Humanos' },
      ],
    })
  })

  it('deja fuera una línea en blanco', () => {
    expect(
      toSetCertificateSignersRequest([
        { nombre: 'Ana Pérez', cargo: 'Ministra' },
        { nombre: '  ', cargo: '' },
      ]),
    ).toEqual({ signers: [{ name: 'Ana Pérez', title: 'Ministra' }] })
  })

  it('manda una lista vacía cuando las dos están en blanco, que borra los guardados', () => {
    expect(toSetCertificateSignersRequest([SIN_FIRMANTE, SIN_FIRMANTE])).toEqual({ signers: [] })
  })

  it('manda un firmante a medias, para que el servicio diga qué campo falta', () => {
    expect(
      toSetCertificateSignersRequest([{ nombre: 'Ana Pérez', cargo: ' ' }, SIN_FIRMANTE]),
    ).toEqual({
      signers: [{ name: 'Ana Pérez', title: '' }],
    })
  })
})

describe('firmantesDelCertificado', () => {
  it('parte de los dos guardados', () => {
    expect(
      firmantesDelCertificado([
        { name: 'Ana Pérez', title: 'Ministra de Turismo' },
        { name: 'Luis Gómez', title: 'Director de Recursos Humanos' },
      ]),
    ).toEqual([
      { nombre: 'Ana Pérez', cargo: 'Ministra de Turismo' },
      { nombre: 'Luis Gómez', cargo: 'Director de Recursos Humanos' },
    ])
  })

  it('completa con una línea en blanco cuando hay uno solo', () => {
    expect(firmantesDelCertificado([{ name: 'Ana Pérez', title: 'Ministra' }])).toEqual([
      { nombre: 'Ana Pérez', cargo: 'Ministra' },
      SIN_FIRMANTE,
    ])
  })

  it('da dos líneas en blanco cuando no hay ninguno', () => {
    expect(firmantesDelCertificado([])).toEqual([SIN_FIRMANTE, SIN_FIRMANTE])
  })
})

describe('lineaEnBlanco', () => {
  it('es una línea sin nombre ni cargo, contando solo espacios como nada', () => {
    expect(lineaEnBlanco({ nombre: '  ', cargo: '' })).toBe(true)
  })

  it('no es una línea con solo el cargo', () => {
    expect(lineaEnBlanco({ nombre: '', cargo: 'Ministra' })).toBe(false)
  })
})
