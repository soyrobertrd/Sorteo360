import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import type { RaffleParticipantInput } from '@/api/types'
import { getBrandColors } from '@/lib/branding'
import type { PremioImportado } from './premioFile'

/**
 * Datos de simulación para ensayar un sorteo o enseñarlo en una demo: 100 empleados, 50 premios y
 * 2 premios principales. Los premios principales llevan foto, así que el tablero los sortea uno a
 * uno. Todo pasa por las mismas rutas que una carga real (padrón, importación de premios y foto),
 * de modo que funciona igual en modo demo y contra el servidor.
 */

export const SIMULACION = { empleados: 100, premios: 50, principales: 2 } as const

const NOMBRES = [
  'María',
  'José',
  'Ana',
  'Luis',
  'Carmen',
  'Juan',
  'Rosa',
  'Pedro',
  'Yokasta',
  'Ramón',
  'Altagracia',
  'Miguel',
  'Mercedes',
  'Rafael',
  'Josefina',
  'Francisco',
  'Margarita',
  'Manuel',
  'Juana',
  'Carlos',
  'Dulce',
  'Víctor',
  'Esther',
  'Domingo',
  'Lucía',
]
const APELLIDOS = [
  'Rodríguez',
  'Pérez',
  'Martínez',
  'García',
  'Santana',
  'Reyes',
  'Peña',
  'Jiménez',
  'Díaz',
  'Mejía',
  'Rosario',
  'Núñez',
  'Castillo',
  'Ramírez',
  'Féliz',
  'Báez',
  'Almonte',
  'Tavárez',
  'Guzmán',
  'Vásquez',
]
const DEPARTAMENTOS = [
  'Banca Personal',
  'Tesorería',
  'Tecnología',
  'Recursos Humanos',
  'Operaciones',
  'Riesgo',
  'Contabilidad',
  'Mercadeo',
]
const UBICACIONES = [
  'Oficina Principal',
  'Santiago',
  'La Romana',
  'San Pedro de Macorís',
  'Puerto Plata',
  'Higüey',
]

/** Determinista: la misma simulación cada vez, para que dos ensayos se puedan comparar. */
export function empleadosDeSimulacion(n: number = SIMULACION.empleados): RaffleParticipantInput[] {
  return Array.from({ length: n }, (_, i) => {
    const firstName = NOMBRES[(i * 7) % NOMBRES.length]
    const lastName = `${APELLIDOS[(i * 3) % APELLIDOS.length]} ${APELLIDOS[(i * 11 + 5) % APELLIDOS.length]}`
    const codigo = String(1001 + i).padStart(7, '0')
    return {
      fullName: `${firstName} ${lastName}`,
      firstName,
      lastName,
      email: `empleado${codigo}@simulacion.sorteo360.local`,
      department: DEPARTAMENTOS[i % DEPARTAMENTOS.length],
      company: DEPARTAMENTOS[i % DEPARTAMENTOS.length],
      location: UBICACIONES[(i * 5) % UBICACIONES.length],
      employeeCode: codigo,
      checkedIn: true,
    }
  })
}

/** Los 2 principales van en su propia ronda, la última: es el cierre de la fiesta. */
export const PRINCIPALES = ['Televisor 65" 4K', 'Viaje a Punta Cana para dos'] as const

/**
 * Los premios en dos rondas nuevas, después de las que ya tenga el sorteo: los 50 en la primera y
 * los 2 principales en la segunda, para no mezclarse con lo que ya estaba cargado.
 */
export function premiosDeSimulacion(rondasExistentes = 0): PremioImportado[] {
  const base = rondasExistentes
  return (
    [
      { ronda: 1, nombre: '5,000', valor: 'RD$5,000.00', cantidad: 25, patrocinador: null },
      { ronda: 1, nombre: '10,000', valor: 'RD$10,000.00', cantidad: 15, patrocinador: null },
      {
        ronda: 1,
        nombre: 'Canasta Navideña',
        valor: 'RD$7,500.00',
        cantidad: 7,
        patrocinador: 'Supermercado Aliado',
      },
      { ronda: 1, nombre: 'Tablet', valor: 'RD$18,000.00', cantidad: 3, patrocinador: null },
      { ronda: 2, nombre: PRINCIPALES[0], valor: 'RD$85,000.00', cantidad: 1, patrocinador: null },
      {
        ronda: 2,
        nombre: PRINCIPALES[1],
        valor: 'RD$60,000.00',
        cantidad: 1,
        patrocinador: 'Agencia de Viajes Aliada',
      },
    ] satisfies PremioImportado[]
  ).map((p) => ({ ...p, ronda: base + p.ronda }))
}

/** Una foto de ejemplo dibujada aquí (PNG), sin depender de internet. */
async function fotoDeEjemplo(texto: string, simbolo: string): Promise<File> {
  const canvas = document.createElement('canvas')
  canvas.width = 640
  canvas.height = 480
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('El navegador no puede dibujar la foto de ejemplo.')
  const { primary, accent } = getBrandColors()
  const g = ctx.createLinearGradient(0, 0, 640, 480)
  g.addColorStop(0, primary)
  g.addColorStop(1, accent)
  ctx.fillStyle = g
  ctx.beginPath()
  ctx.roundRect(20, 20, 600, 440, 40)
  ctx.fill()
  ctx.fillStyle = '#ffffff'
  ctx.textAlign = 'center'
  ctx.font = '160px sans-serif'
  ctx.fillText(simbolo, 320, 250)
  ctx.font = 'bold 40px sans-serif'
  ctx.fillText(texto, 320, 360, 560)
  const blob = await new Promise<Blob | null>((ok) => canvas.toBlob(ok, 'image/png'))
  if (!blob) throw new Error('No se pudo crear la foto de ejemplo.')
  return new File([blob], 'premio-principal.png', { type: 'image/png' })
}

/**
 * Carga la simulación en un sorteo en borrador: el padrón (todos presentes), los premios en una
 * sola importación y la foto de los dos principales.
 */
export function useCargarSimulacion(eventId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      await api.importRaffleParticipants(eventId, {
        participants: empleadosDeSimulacion(),
        markCheckedIn: true,
        source: 'manual',
      })
      const antes = await api.getRaffle(eventId)
      const ultimaRonda = Math.max(0, ...antes.rounds.map((r) => r.number))
      await api.importPrizes(eventId, {
        prizes: premiosDeSimulacion(ultimaRonda).map((p) => ({
          roundNumber: p.ronda,
          name: p.nombre,
          value: p.valor,
          quantity: p.cantidad,
          sponsorName: p.patrocinador,
        })),
      })
      const sorteo = await api.getRaffle(eventId)
      const premios = sorteo.rounds.flatMap((r) => r.prizes)
      const simbolos = ['📺', '✈️']
      for (const [i, nombre] of PRINCIPALES.entries()) {
        const ids = premios.filter((p) => p.name === nombre && !p.imageUrl).map((p) => p.id)
        if (ids.length)
          await api.setPrizeImage(eventId, ids, await fotoDeEjemplo(nombre, simbolos[i]))
      }
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['raffles'] }),
  })
}
