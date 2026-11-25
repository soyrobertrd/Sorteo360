import type { PagedResponse } from '@/api/types'

/**
 * Lee un listado paginado entero, página tras página, hasta juntar `total` o recibir una página
 * vacía (así un `total` que cambia a mitad de la lectura no deja el bucle girando).
 */
export async function leerTodasLasPaginas<T>(
  leerPagina: (page: number) => Promise<PagedResponse<T>>,
): Promise<T[]> {
  const items: T[] = []
  for (let page = 1; ; page++) {
    const res = await leerPagina(page)
    items.push(...res.items)
    if (res.items.length === 0 || items.length >= res.total) return items
  }
}
