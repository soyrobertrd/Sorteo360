import { beforeEach, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/client'
import { encolar, leerCola, vaciarCola } from './colaCheckin'

const sinRed = () => new ApiError('sin red', 0)

// Un localStorage en memoria: el de Node 22 sin `--localstorage-file` lanza al usarse.
beforeEach(() => {
  const datos = new Map<string, string>()
  vi.stubGlobal('localStorage', {
    getItem: (k: string) => datos.get(k) ?? null,
    setItem: (k: string, v: string) => void datos.set(k, v),
    removeItem: (k: string) => void datos.delete(k),
    get length() {
      return datos.size
    },
  })
})

it('guarda cada código una sola vez, en orden', () => {
  encolar('ev', 'QR-1')
  encolar('ev', 'QR-2')
  encolar('ev', 'QR-1')
  expect(leerCola('ev').map((p) => p.codigo)).toEqual(['QR-1', 'QR-2'])
  expect(leerCola('otro')).toEqual([])
})

it('se detiene en el primer fallo de red y descarta los rechazos del servidor', async () => {
  for (const c of ['QR-1', 'MALO', 'QR-3', 'QR-4']) encolar('ev', c)
  const enviados: string[] = []
  const r = await vaciarCola('ev', async (c) => {
    if (c === 'MALO') throw new ApiError('no existe', 404)
    if (c === 'QR-3') throw sinRed()
    enviados.push(c)
  })
  expect(r).toEqual({ enviados: 1, rechazados: ['MALO'], pendientes: 2 })
  expect(enviados).toEqual(['QR-1'])
  expect(leerCola('ev').map((p) => p.codigo)).toEqual(['QR-3', 'QR-4'])

  const r2 = await vaciarCola('ev', async () => undefined)
  expect(r2).toEqual({ enviados: 2, rechazados: [], pendientes: 0 })
  expect(localStorage.length).toBe(0)
})
