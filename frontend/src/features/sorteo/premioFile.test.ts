import { describe, expect, it } from 'vitest'
import {
  detectarAsignacion,
  leerMonto,
  leerPremios,
  limpiarMatriz,
  textoAMatriz,
} from './premioFile'

describe('premioFile', () => {
  it('reconoce el encabezado en cualquier orden y agrupa filas seguidas iguales', () => {
    const m = textoAMatriz(
      '\uFEFFMonto;Premio;Ronda\nRD$ 5,000;Canasta;1\n5000;Canasta;1\n;Bono;2\n10.000,50;TV;x\n;;\n',
    )
    const { asignacion, conEncabezado } = detectarAsignacion(m)
    expect(conEncabezado).toBe(true)
    expect(asignacion).toEqual({
      ronda: 2,
      premio: 1,
      valor: 0,
      cantidad: null,
      patrocinador: null,
    })
    const r = leerPremios(m, asignacion, conEncabezado)
    expect(r.premios).toEqual([
      { ronda: 1, nombre: 'Canasta', valor: 'RD$5,000.00', cantidad: 2, patrocinador: null },
      { ronda: 2, nombre: 'Bono', valor: null, cantidad: 1, patrocinador: null },
    ])
    expect(r.problemas).toEqual([
      { fila: 5, mensaje: 'La ronda "x" no es un número entero entre 1 y 50.' },
    ])
  })

  it('no rechaza un premio por su valor: vacío queda sin valor y un texto se guarda tal cual', () => {
    const m = textoAMatriz('Ronda;Premio;Valor\n1;Bono;\n1;Viaje;Pasaje doble\n2;TV;-\n')
    const d = detectarAsignacion(m)
    const r = leerPremios(m, d.asignacion, d.conEncabezado)
    expect(r.problemas).toEqual([])
    expect(r.premios.map((p) => [p.nombre, p.valor])).toEqual([
      ['Bono', null],
      ['Viaje', 'Pasaje doble'],
      ['TV', '-'],
    ])
  })

  it('sin encabezado propone Ronda, Premio y Valor en las primeras columnas', () => {
    const m = limpiarMatriz([
      [1, 'Laptop', 45000],
      [null, null],
      [2, '', 3],
    ])
    expect(m).toHaveLength(2)
    const d = detectarAsignacion(m)
    expect(d).toEqual({
      asignacion: { ronda: 0, premio: 1, valor: 2, cantidad: null, patrocinador: null },
      conEncabezado: false,
    })
    const r = leerPremios(m, d.asignacion, false)
    expect(r.premios).toEqual([
      { ronda: 1, nombre: 'Laptop', valor: 'RD$45,000.00', cantidad: 1, patrocinador: null },
    ])
    expect(r.problemas[0]).toEqual({ fila: 2, mensaje: 'Falta el nombre del premio.' })
  })

  it('reconoce encabezados que empiezan por el nombre del campo', () => {
    const m = [['Monto del premio', 'Descripción del premio', 'No. Ronda']]
    expect(detectarAsignacion(m).asignacion).toEqual({
      ronda: 2,
      premio: 1,
      valor: 0,
      cantidad: null,
      patrocinador: null,
    })
  })

  it('lee el patrocinador, opcional, y no junta premios de patrocinadores distintos', () => {
    const m = textoAMatriz(
      'Ronda;Premio;Cantidad;Patrocinador\n1;Bono;2;Brugal\n1;Bono;3;Brugal\n1;Bono;1;\n1;Bono;1;Presidente\n',
    )
    const d = detectarAsignacion(m)
    expect(d.asignacion.patrocinador).toBe(3)
    const r = leerPremios(m, d.asignacion, d.conEncabezado)
    expect(r.premios.map((p) => [p.cantidad, p.patrocinador])).toEqual([
      [5, 'Brugal'],
      [1, null],
      [1, 'Presidente'],
    ])
    expect(r.problemas).toEqual([])
  })

  it('lee la cantidad: un premio repartido en rondas', () => {
    const m = textoAMatriz(
      'Ronda;Premio;Valor;Cantidad\n1;Bono;10000;25\n2;Bono;10000;25\n3;Bono;10000;1,000\n4;Bono;;0\n',
    )
    const d = detectarAsignacion(m)
    expect(d.asignacion.cantidad).toBe(3)
    const r = leerPremios(m, d.asignacion, d.conEncabezado)
    expect(r.premios.map((p) => [p.ronda, p.cantidad])).toEqual([
      [1, 25],
      [2, 25],
      [3, 1000],
    ])
    expect(r.problemas[0].fila).toBe(5)
  })

  it('lee montos con miles y decimales en ambos formatos', () => {
    expect(leerMonto('RD$ 1,250.75')).toBe(1250.75)
    expect(leerMonto('1.250,75')).toBe(1250.75)
    expect(leerMonto('10,000')).toBe(10000)
    expect(leerMonto('10.000')).toBe(10000)
    expect(leerMonto('sin valor')).toBeNull()
  })
})
