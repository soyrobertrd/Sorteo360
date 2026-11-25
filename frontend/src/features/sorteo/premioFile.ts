import { normalizeHeader, splitDelimitedLine } from './participantFile'

/**
 * Lectura del listado de premios (Excel ya convertido a matriz, o CSV). Pura: la vista previa, la
 * asignación de columnas y los errores se prueban sin pantalla.
 */

export type CampoPremio = 'ronda' | 'premio' | 'valor' | 'cantidad' | 'patrocinador'

/** Qué columna (índice) alimenta cada campo; `null` si ninguna. */
export type AsignacionColumnas = Record<CampoPremio, number | null>

export interface PremioImportado {
  ronda: number
  nombre: string
  /** Ya formateado en pesos (`RD$10,000.00`), o `null` si la fila no trae valor. */
  valor: string | null
  /**
   * Unidades de ese premio en esa ronda: la columna Cantidad (100 bonos = una fila con 100), o 1
   * por fila si no hay columna. Filas seguidas iguales se suman.
   */
  cantidad: number
  /** El patrocinador como texto libre, o `null`: no es obligatorio. */
  patrocinador: string | null
}

export interface ProblemaPremio {
  /** Fila de la tabla leída (sin filas vacías), contando el encabezado; la primera es la 1. */
  fila: number
  mensaje: string
}

const ALIAS: Record<CampoPremio, string[]> = {
  ronda: ['ronda', 'round', 'no ronda', 'numero de ronda', 'n ronda'],
  premio: ['premio', 'premios', 'nombre', 'nombre del premio', 'descripcion', 'articulo', 'prize'],
  valor: ['valor', 'monto', 'precio', 'valor rd', 'monto rd', 'valor en pesos', 'importe', 'value'],
  cantidad: [
    'cantidad',
    'cant',
    'unidades',
    'qty',
    'quantity',
    'cantidad de premios',
    'numero de premios',
  ],
  patrocinador: [
    'patrocinador',
    'nombre del patrocinador',
    'patrocinado por',
    'patrocinio',
    'sponsor',
    'auspiciador',
    'auspicio',
  ],
}

/** El tope de unidades por fila; el de toda la importación lo pone el servicio (10.000). */
const MAX_CANTIDAD = 10_000

/** CSV/TXT con separador a matriz: `;`, tabulador, `|` o `,`, el primero de la primera línea. */
export function textoAMatriz(texto: string): string[][] {
  const lineas = texto.replace(/^\uFEFF/, '').split(/\r?\n/)
  const primera = lineas.find((l) => l.trim() !== '') ?? ''
  const sep = [';', '\t', '|', ','].find((d) => primera.includes(d))
  return limpiarMatriz(lineas.map((l) => (sep ? splitDelimitedLine(l, sep) : [l])))
}

/** Celdas como texto y sin filas vacías: lo que se muestra y lo que se lee es lo mismo. */
export function limpiarMatriz(matriz: unknown[][]): string[][] {
  return matriz
    .map((fila) => (fila ?? []).map((c) => (c == null ? '' : String(c).trim())))
    .filter((fila) => fila.some((c) => c !== ''))
}

/**
 * Reconoce el encabezado: si la primera fila nombra alguna columna conocida, es encabezado y se
 * asigna; si no, se propone Ronda / Premio / Valor / Cantidad / Patrocinador en las cinco primeras columnas.
 */
export function detectarAsignacion(matriz: string[][]): {
  asignacion: AsignacionColumnas
  conEncabezado: boolean
} {
  const encabezado = (matriz[0] ?? []).map(normalizeHeader)
  const asignacion: AsignacionColumnas = {
    ronda: null,
    premio: null,
    valor: null,
    cantidad: null,
    patrocinador: null,
  }
  const libre = (idx: number) => !Object.values(asignacion).includes(idx)
  // Primero el nombre exacto; después el que empieza por él ("Monto del premio" es el valor).
  for (const coincide of [
    (h: string, a: string) => h === a,
    (h: string, a: string) => h.startsWith(a + ' '),
  ]) {
    for (const campo of Object.keys(ALIAS) as CampoPremio[]) {
      if (asignacion[campo] !== null) continue
      const i = encabezado.findIndex(
        (h, idx) => libre(idx) && ALIAS[campo].some((a) => coincide(h, a)),
      )
      if (i >= 0) asignacion[campo] = i
    }
  }
  if (Object.values(asignacion).some((v) => v !== null)) return { asignacion, conEncabezado: true }
  const columnas = Math.max(0, ...matriz.map((f) => f.length))
  return {
    asignacion: {
      ronda: columnas > 0 ? 0 : null,
      premio: columnas > 1 ? 1 : null,
      valor: columnas > 2 ? 2 : null,
      cantidad: columnas > 3 ? 3 : null,
      patrocinador: columnas > 4 ? 4 : null,
    },
    conEncabezado: false,
  }
}

/** `RD$ 10,000.50`, `10000`, `10.000,50` → número; `null` si no hay número. */
export function leerMonto(celda: string): number | null {
  let s = celda.replace(/[^\d.,-]/g, '')
  if (!/\d/.test(s)) return null
  const ultimaComa = s.lastIndexOf(',')
  const ultimoPunto = s.lastIndexOf('.')
  // El separador que aparece último y deja 1-2 decimales es el decimal; el otro es de miles.
  if (ultimaComa > ultimoPunto && s.length - ultimaComa - 1 <= 2) {
    s = s.replace(/\./g, '').replace(',', '.')
  } else if (ultimoPunto > ultimaComa && s.length - ultimoPunto - 1 <= 2) {
    s = s.replace(/,/g, '')
  } else {
    s = s.replace(/[.,]/g, '')
  }
  const n = Number(s)
  return Number.isFinite(n) ? n : null
}

const PESOS = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
export const formatoPesos = (n: number) => `RD$${PESOS.format(n)}`

export function leerPremios(
  matriz: string[][],
  asignacion: AsignacionColumnas,
  conEncabezado: boolean,
): { premios: PremioImportado[]; problemas: ProblemaPremio[] } {
  const premios: PremioImportado[] = []
  const problemas: ProblemaPremio[] = []
  const celda = (fila: string[], campo: CampoPremio) => {
    const i = asignacion[campo]
    return i == null ? '' : (fila[i] ?? '').trim()
  }
  matriz.forEach((fila, idx) => {
    if (conEncabezado && idx === 0) return
    const numeroFila = idx + 1
    const nombre = celda(fila, 'premio')
    const textoRonda = celda(fila, 'ronda')
    const textoValor = celda(fila, 'valor')
    const textoCantidad = celda(fila, 'cantidad')
    const patrocinador = celda(fila, 'patrocinador').slice(0, 200) || null
    if (!nombre) {
      problemas.push({ fila: numeroFila, mensaje: 'Falta el nombre del premio.' })
      return
    }
    const ronda = textoRonda === '' && asignacion.ronda == null ? 1 : Number(textoRonda)
    if (!Number.isInteger(ronda) || ronda < 1 || ronda > 50) {
      problemas.push({
        fila: numeroFila,
        mensaje: `La ronda "${textoRonda}" no es un número entero entre 1 y 50.`,
      })
      return
    }
    // El valor no es obligatorio: un monto se formatea en pesos; un texto ("Viaje", "N/A") se
    // guarda tal cual y nunca tumba la fila.
    let valor: string | null = null
    if (textoValor) {
      const monto = leerMonto(textoValor)
      valor = monto != null && monto >= 0 ? formatoPesos(monto) : textoValor.slice(0, 100)
    }
    // Sin columna o vacía, la fila es un premio; "1,000" o "1.000" se leen como mil.
    const cantidad = textoCantidad === '' ? 1 : Number(textoCantidad.replace(/[.,\s]/g, ''))
    if (!Number.isInteger(cantidad) || cantidad < 1 || cantidad > MAX_CANTIDAD) {
      problemas.push({
        fila: numeroFila,
        mensaje: `La cantidad "${textoCantidad}" no es un número entero entre 1 y ${MAX_CANTIDAD.toLocaleString('es-DO')}.`,
      })
      return
    }
    const previo = premios[premios.length - 1]
    if (
      previo?.ronda === ronda &&
      previo.nombre === nombre &&
      previo.valor === valor &&
      previo.patrocinador === patrocinador
    ) {
      previo.cantidad += cantidad
    } else {
      premios.push({ ronda, nombre, valor, cantidad, patrocinador })
    }
  })
  return { premios, problemas }
}
