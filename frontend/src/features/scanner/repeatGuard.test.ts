import { describe, expect, it } from 'vitest'
import { freshCodes } from './repeatGuard'

const A = 'RD-7K3M-9XQ2'
const B = 'REF-2026-00427'

/** Lo que pasa de cada fotograma, en orden, con un mismo `lastSeen` para toda la secuencia. */
function frames(...seq: [number, string[]][]) {
  const lastSeen = new Map<string, number>()
  return seq.map(([at, codes]) => freshCodes(lastSeen, codes, at))
}

describe('freshCodes', () => {
  it('pasa un código la primera vez que se ve', () => {
    expect(frames([10_000, [A]])).toEqual([[A]])
  })

  it('descarta el mismo código dentro de los dos segundos', () => {
    expect(frames([10_000, [A]], [11_999, [A]])).toEqual([[A], []])
  })

  it('vuelve a pasar un código que dejó de verse dos segundos', () => {
    expect(frames([10_000, [A]], [12_000, [A]])).toEqual([[A], [A]])
  })

  it('sigue descartando un código que la cámara no deja de ver', () => {
    expect(frames([0, [A]], [1_500, [A]], [3_000, [A]], [4_500, [A]])).toEqual([[A], [], [], []])
  })

  it('con dos códigos en el cuadro, cada uno pasa una sola vez', () => {
    expect(frames([0, [A, B]], [200, [B, A]], [400, [A]], [600, [B]])).toEqual([[A, B], [], [], []])
  })

  it('no descarta otro código aunque llegue enseguida', () => {
    expect(frames([10_000, [A]], [10_100, [B]])).toEqual([[A], [B]])
  })

  it('un código repetido dentro del mismo fotograma pasa una vez', () => {
    expect(frames([0, [A, A]])).toEqual([[A]])
  })
})
