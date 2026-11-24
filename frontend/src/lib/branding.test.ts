import { expect, it } from 'vitest'
import { mixHex, safeHexColor } from './branding'

it('mixHex mezcla en la proporción pedida', () => {
  expect(mixHex('#000000', '#ffffff', 0)).toBe('#000000')
  expect(mixHex('#000000', '#ffffff', 1)).toBe('#ffffff')
  expect(mixHex('#0d3b7d', '#ffffff', 0.5)).toBe('#869dbe')
})

it('safeHexColor descarta lo que no es #rrggbb', () => {
  expect(safeHexColor('#ABCDEF', '#000000')).toBe('#abcdef')
  expect(safeHexColor('red', '#000000')).toBe('#000000')
  expect(safeHexColor('#abc', '#000000')).toBe('#000000')
})
