import { describe, expect, it } from 'vitest'
import { matchesAccept } from './FileDropzone'

const archivo = (name: string, type = '') => new File(['x'], name, { type })

describe('matchesAccept', () => {
  it('acepta por extensión sin importar mayúsculas', () => {
    expect(matchesAccept(archivo('Padron.XLSX'), '.xlsx,.csv')).toBe(true)
    expect(matchesAccept(archivo('foto.png'), '.xlsx,.csv')).toBe(false)
  })

  it('acepta por familia de tipo MIME', () => {
    expect(matchesAccept(archivo('logo.svg', 'image/svg+xml'), 'image/*')).toBe(true)
    expect(matchesAccept(archivo('doc.pdf', 'application/pdf'), 'image/*')).toBe(false)
  })
})
