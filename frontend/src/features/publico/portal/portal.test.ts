import { describe, expect, it } from 'vitest'
import { fragmentParam } from './portal'

describe('fragmentParam, el token del fragmento del enlace', () => {
  it('lee el token de #t=', () => {
    expect(fragmentParam('#t=abc-123', 't')).toBe('abc-123')
  })

  it('lee la clave pedida entre varias', () => {
    expect(fragmentParam('#u=baja&t=abc', 't')).toBe('abc')
  })

  it.each(['', '#', '#t=', '#t=%20%20', '#u=otro'])('da null con %j', (hash) => {
    expect(fragmentParam(hash, 't')).toBeNull()
  })
})
