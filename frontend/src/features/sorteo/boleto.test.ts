import { describe, expect, it } from 'vitest'
import { formatoBoleto, nuevoBoleto } from './boleto'

describe('boleto', () => {
  it('formatea seis dígitos con guion', () => {
    expect(formatoBoleto(7)).toBe('000-007')
    expect(formatoBoleto(482917)).toBe('482-917')
  })

  it('no repite un número ya usado', () => {
    const secuencia = [0.1, 0.1, 0.2]
    let i = 0
    const azar = () => secuencia[i++]
    expect(nuevoBoleto(new Set(['100-000']), azar)).toBe('200-000')
  })

  it('genera números distintos con el azar real', () => {
    const usados = new Set<string>()
    for (let k = 0; k < 500; k++) usados.add(nuevoBoleto(usados))
    expect(usados.size).toBe(500)
  })
})
