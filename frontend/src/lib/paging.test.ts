import { describe, expect, it } from 'vitest'
import { leerTodasLasPaginas } from './paging'

const paginas = (total: number, pageSize: number) => (page: number) =>
  Promise.resolve({
    total,
    page,
    pageSize,
    items: Array.from(
      { length: Math.max(0, Math.min(pageSize, total - (page - 1) * pageSize)) },
      (_, i) => (page - 1) * pageSize + i,
    ),
  })

describe('leerTodasLasPaginas', () => {
  it('junta todas las páginas, no solo la primera', async () => {
    const items = await leerTodasLasPaginas(paginas(250, 100))
    expect(items).toHaveLength(250)
    expect(items.at(-1)).toBe(249)
  })

  it('con una sola página no pide más', async () => {
    let llamadas = 0
    await leerTodasLasPaginas((p) => {
      llamadas++
      return paginas(3, 100)(p)
    })
    expect(llamadas).toBe(1)
  })

  it('se detiene ante una página vacía aunque el total prometa más', async () => {
    const items = await leerTodasLasPaginas((page) =>
      Promise.resolve({ total: 500, page, pageSize: 100, items: page === 1 ? [1, 2] : [] }),
    )
    expect(items).toEqual([1, 2])
  })
})
