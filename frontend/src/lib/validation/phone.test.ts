import { describe, expect, it } from 'vitest'
import { esTelefonoValido } from './phone'

describe('esTelefonoValido, la regla de crm-service', () => {
  it.each(['8095550', '+1 (809) 555-0199', '+34 912 345 678 9012', '', '   '])(
    'acepta %j',
    (telefono) => {
      expect(esTelefonoValido(telefono)).toBe(true)
    },
  )

  it.each(['809-555-01a9', '809.555.0199', '555-01', '+1 (809) 555-0199 1234'])(
    'rechaza %j',
    (telefono) => {
      expect(esTelefonoValido(telefono)).toBe(false)
    },
  )

  it('acepta un teléfono ausente', () => {
    expect(esTelefonoValido(null)).toBe(true)
    expect(esTelefonoValido(undefined)).toBe(true)
  })
})
