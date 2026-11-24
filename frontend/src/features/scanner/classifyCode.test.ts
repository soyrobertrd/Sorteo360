import { describe, expect, it } from 'vitest'
import { classifyCode } from './classifyCode'

// Un QrCode real: 32 bytes en base64url, 43 caracteres.
const QR = 'q8Zr3vT0bX9-_kLmN2pQ4sU6wY8aC1eG3iK5oM7qS9u'

describe('classifyCode', () => {
  it('lee una entrada de la URL del boleto', () => {
    expect(classifyCode(`https://sorteo360.example.com/publico/entrada/${QR}`)).toEqual({
      kind: 'ticket',
      qrCode: QR,
    })
  })

  it('lee una entrada de la URL aunque el host sea de otro ambiente', () => {
    expect(classifyCode(`http://otro.example.com:8080/publico/entrada/${QR}?x=1#y`)).toEqual({
      kind: 'ticket',
      qrCode: QR,
    })
  })

  it('lee una entrada del QrCode suelto', () => {
    expect(classifyCode(`  ${QR}\n`)).toEqual({ kind: 'ticket', qrCode: QR })
  })

  it('no reconoce un código de 42 caracteres', () => {
    expect(classifyCode(QR.slice(1))).toEqual({ kind: 'unrecognised' })
  })

  it.each(['RD-7K3M-9XQ2', 'rd-7k3m-9xq2', 'RD 7K3M 9XQ2', 'rd7k3m9xq2', ' RD-7K3M 9xq2 '])(
    'lee un código de premio de %j',
    (input) => {
      expect(classifyCode(input)).toEqual({ kind: 'prize', code: 'RD-7K3M-9XQ2' })
    },
  )

  it.each(['RD-7K3M-9XQI', 'RD-7K3M-9XQL', 'RD-7K3M-9XQO', 'RD-7K3M-9XQU'])(
    'no reconoce un código de premio con una letra fuera del alfabeto: %j',
    (input) => {
      expect(classifyCode(input)).toEqual({ kind: 'unrecognised' })
    },
  )

  it.each(['RD-7K3M-9XQ', 'RD-7K3M-9XQ2A', 'XD-7K3M-9XQ2'])(
    'no reconoce un código de premio mal formado: %j',
    (input) => {
      expect(classifyCode(input)).toEqual({ kind: 'unrecognised' })
    },
  )

  it.each([
    ['REF-2026-00427', 'REF-2026-00427'],
    [' ref-2026-7 ', 'REF-2026-7'],
  ])('lee una Reference de %j', (input, reference) => {
    expect(classifyCode(input)).toEqual({ kind: 'reference', reference })
  })

  it.each(['REF-26-00427', 'REF-2026-', 'REF-2026-00A27', 'REF 2026 00427'])(
    'no reconoce una Reference mal formada: %j',
    (input) => {
      expect(classifyCode(input)).toEqual({ kind: 'unrecognised' })
    },
  )

  it.each(['', '   ', 'María García', 'https://otro.example.com/publico/registro/abc'])(
    'no reconoce %j',
    (input) => {
      expect(classifyCode(input)).toEqual({ kind: 'unrecognised' })
    },
  )
})
