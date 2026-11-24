import { describe, expect, it, vi } from 'vitest'
import { cssUrl, safeUrl } from './safeUrl'

vi.stubGlobal('window', { location: { origin: 'https://app.test' } })

describe('safeUrl', () => {
  it('acepta https, http, mailto y blob', () => {
    expect(safeUrl('https://sorteo360.example.com')).toBe('https://sorteo360.example.com')
    expect(safeUrl('http://a.b')).toBe('http://a.b')
    expect(safeUrl('mailto:x@y.z')).toBe('mailto:x@y.z')
    expect(safeUrl('blob:https://app.test/1')).toBe('blob:https://app.test/1')
  })
  it('rechaza javascript y data', () => {
    expect(safeUrl('javascript:alert(1)')).toBeUndefined()
    expect(safeUrl(' JavaScript:alert(1)')).toBeUndefined()
    expect(safeUrl('data:text/html,x')).toBeUndefined()
  })
  it('soloHttps deja pasar solo https', () => {
    expect(safeUrl('http://a.b', true)).toBeUndefined()
    expect(safeUrl('https://a.b', true)).toBe('https://a.b')
  })
})

describe('cssUrl', () => {
  it('codifica comillas, barras y saltos', () => {
    expect(cssUrl('a") ;x:y("\\\n')).toBe('url("a%22) ;x:y(%22%5C%0A")')
  })
})
