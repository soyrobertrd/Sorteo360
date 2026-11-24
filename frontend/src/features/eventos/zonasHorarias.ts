import { ApiError } from '@/api'
import { apiErrorCodeLabel } from '@/lib/labels'

/**
 * La zona horaria de una ciudad, como id IANA. Es la hora que imprime el acta notarial de un sorteo
 * celebrado allí: el notario la compara con su reloj.
 */
export const ZONA_HORARIA_POR_DEFECTO = 'America/Santo_Domingo'

/**
 * Los países y ciudades que ofrece el formulario de eventos, cada ciudad con su zona. Elegir la
 * ciudad preselecciona la zona; "Otro" no sabe dónde queda y cae en Santo Domingo.
 */
const ZONAS_POR_CIUDAD: Record<string, Record<string, string>> = {
  'República Dominicana': {
    'Santo Domingo': ZONA_HORARIA_POR_DEFECTO,
    'Punta Cana': ZONA_HORARIA_POR_DEFECTO,
    Santiago: ZONA_HORARIA_POR_DEFECTO,
    'Puerto Plata': ZONA_HORARIA_POR_DEFECTO,
    'La Romana': ZONA_HORARIA_POR_DEFECTO,
    Samaná: ZONA_HORARIA_POR_DEFECTO,
    Miches: ZONA_HORARIA_POR_DEFECTO,
    Pedernales: ZONA_HORARIA_POR_DEFECTO,
  },
  'Estados Unidos': {
    'Nueva York': 'America/New_York',
    Boston: 'America/New_York',
    Miami: 'America/New_York',
    Chicago: 'America/Chicago',
    Houston: 'America/Chicago',
    Dallas: 'America/Chicago',
  },
  España: { Madrid: 'Europe/Madrid', Barcelona: 'Europe/Madrid' },
  Canadá: { Toronto: 'America/Toronto', Montreal: 'America/Toronto' },
  Francia: { París: 'Europe/Paris' },
  Otro: { Otro: ZONA_HORARIA_POR_DEFECTO },
}

export const PAISES_CIUDADES: Record<string, string[]> = Object.fromEntries(
  Object.entries(ZONAS_POR_CIUDAD).map(([pais, ciudades]) => [pais, Object.keys(ciudades)]),
)

/** Sin mayúsculas, tildes ni espacios sobrantes: así se compara lo escrito a mano. */
function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .trim()
    .toLowerCase()
}

const zonaPorClave = new Map(
  Object.entries(ZONAS_POR_CIUDAD).flatMap(([pais, ciudades]) =>
    Object.entries(ciudades).map(
      ([ciudad, zona]) => [`${normalizar(pais)}|${normalizar(ciudad)}`, zona] as const,
    ),
  ),
)

/** La zona de una ciudad de la lista, o la de Santo Domingo cuando no está en ella. */
export function zonaHorariaDeCiudad(pais: string, ciudad: string): string {
  return zonaPorClave.get(`${normalizar(pais)}|${normalizar(ciudad)}`) ?? ZONA_HORARIA_POR_DEFECTO
}

function nombreDeZona(zona: string, en: Date, timeZoneName: 'longOffset' | 'longGeneric'): string {
  return (
    new Intl.DateTimeFormat('es', { timeZone: zona, timeZoneName })
      .formatToParts(en)
      .find((parte) => parte.type === 'timeZoneName')?.value ?? ''
  )
}

/**
 * Lo que acompaña al id en el selector: "UTC−05:00 (hora central)". La diferencia es la del día
 * `en`, así que cambia con el horario de verano; el acta la calcula para cada hora que imprime.
 */
export function detalleDeZonaHoraria(zona: string, en: Date): string {
  // Intl escribe "GMT-05:00", o "GMT" a secas en UTC; el acta usa "UTC−05:00", con el signo menos.
  const diferencia = nombreDeZona(zona, en, 'longOffset').replace('GMT', '') || '+00:00'
  const utc = `UTC${diferencia.replace('-', '−')}`
  const nombre = nombreDeZona(zona, en, 'longGeneric')
  // Una zona sin nombre en español devuelve otra vez la diferencia, que ya está escrita.
  return nombre.startsWith('GMT') ? utc : `${utc} (${nombre})`
}

/** La pista bajo el selector, con la diferencia de hoy. Un id a medio escribir no tiene ninguna. */
export function detalleDeZonaHorariaHoy(zona: string): string | undefined {
  try {
    return detalleDeZonaHoraria(zona, new Date())
  } catch {
    return undefined
  }
}

let zonasDelNavegador: Set<string> | undefined

/**
 * Dice si la zona es una de las que ofrece el selector, que son las que conoce el navegador. La lista
 * del navegador y la del servicio no coinciden en todos los alias, así que la zona que la ciudad
 * ya tiene guardada vale aunque el navegador no la traiga: el servicio la aceptó.
 */
export function esZonaHorariaConocida(zona: string, guardada?: string | null): boolean {
  zonasDelNavegador ??= new Set(Intl.supportedValuesOf('timeZone'))
  return zona === guardada || zonasDelNavegador.has(zona)
}

/** El rechazo de la zona va sobre el selector, no en el aviso general del formulario. */
export function errorDeZonaHoraria(error: unknown): string | undefined {
  return error instanceof ApiError && error.code === 'TIME_ZONE_UNKNOWN'
    ? apiErrorCodeLabel.TIME_ZONE_UNKNOWN
    : undefined
}
