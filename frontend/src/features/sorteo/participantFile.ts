import type { RaffleParticipantInput } from '@/api/types'

/**
 * Lectura del padrón que sube el operador (Excel ya convertido a matriz, CSV o TXT). Es pura:
 * no lee archivos ni toca la red, así que la vista previa y sus errores se prueban sin pantalla.
 *
 * Una fila con problema no tumba la carga: se informa con su número de fila del archivo (la
 * primera fila es la 1, como la ve quien abre el Excel) y el resto se importa.
 */

export interface ParticipantFileProblem {
  /** Fila del archivo, contando el encabezado si lo hay. */
  row: number
  message: string
}

export interface ParsedParticipantFile {
  rows: RaffleParticipantInput[]
  problems: ParticipantFileProblem[]
}

export type CampoPadron = Field

type Field =
  | 'fullName'
  | 'firstName'
  | 'lastName'
  | 'email'
  | 'department'
  | 'company'
  | 'location'
  | 'employeeCode'
  | 'phone'
  | 'qrCode'
  | 'checkedIn'

// Encabezados reconocidos, ya normalizados (sin tildes, minúsculas, sin signos). Se comparan
// enteros: una búsqueda por "contiene" confundía "Apellido" con "id".
const HEADER_ALIASES: Record<Field, string[]> = {
  fullName: [
    'nombre completo',
    'nombres y apellidos',
    'fullname',
    'full name',
    'participante',
    'empleado',
    'colaborador',
  ],
  firstName: ['nombre', 'nombres', 'name', 'first name', 'firstname', 'primer nombre'],
  lastName: ['apellido', 'apellidos', 'last name', 'lastname', 'surname'],
  email: ['correo', 'email', 'e mail', 'mail', 'correo electronico', 'correo institucional'],
  department: ['departamento', 'department', 'area', 'direccion', 'gerencia', 'unidad'],
  company: ['empresa', 'company', 'filial'],
  location: [
    'ubicacion',
    'location',
    'localidad',
    'sucursal',
    'oficina',
    'branch',
    'sede',
    'lugar',
  ],
  employeeCode: [
    'codigo',
    'codigo empleado',
    'codigo de empleado',
    'employee code',
    'employeecode',
    'id empleado',
    'id de empleado',
    'id',
    'legajo',
    'numero de empleado',
    'no empleado',
    'ficha',
    'cedula',
    'no cedula',
    'numero de cedula',
    'documento',
  ],
  phone: ['telefono', 'phone', 'celular', 'movil', 'whatsapp'],
  qrCode: ['qr', 'qrcode', 'qr code', 'codigo qr'],
  checkedIn: ['checkin', 'check in', 'checkedin', 'asistencia', 'presente', 'asistio'],
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function normalizeHeader(h: string): string {
  return h
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[_\-.#:/]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

function parseBooleanCell(val: unknown): boolean | undefined {
  if (typeof val === 'boolean') return val
  if (typeof val === 'number') return val === 1
  const text = normalizeHeader(String(val ?? ''))
  if (!text) return undefined
  if (['si', 'yes', 'true', '1', 'presente', 'ok', 'x', 'checkin', 'check in'].includes(text))
    return true
  if (['no', 'false', '0', 'pendiente', 'ausente'].includes(text)) return false
  return undefined
}

/** Los campos que se pueden asignar a una columna en la carga, con su nombre por omisión. */
export const CAMPOS_PADRON: { campo: Field; etiqueta: string }[] = [
  { campo: 'employeeCode', etiqueta: 'ID de empleado' },
  { campo: 'fullName', etiqueta: 'Nombre completo' },
  { campo: 'firstName', etiqueta: 'Nombre' },
  { campo: 'lastName', etiqueta: 'Apellido' },
  { campo: 'department', etiqueta: 'Departamento' },
  { campo: 'location', etiqueta: 'Ubicación' },
  { campo: 'company', etiqueta: 'Empresa' },
  { campo: 'email', etiqueta: 'Correo' },
  { campo: 'phone', etiqueta: 'Teléfono' },
]

export type ColumnasPadron = Record<Field, number>

/** La columna de cada campo, o -1. Un encabezado solo sirve a un campo. */
export function mapHeaders(headers: string[]): ColumnasPadron {
  const norm = headers.map((h) => normalizeHeader(String(h ?? '')))
  const used = new Set<number>()
  const out = {} as Record<Field, number>
  for (const field of Object.keys(HEADER_ALIASES) as Field[]) {
    const idx = norm.findIndex((h, i) => !used.has(i) && HEADER_ALIASES[field].includes(h))
    out[field] = idx
    if (idx >= 0) used.add(idx)
  }
  return out
}

const isEmptyRow = (row: unknown[]) =>
  row.every((cell) => cell === null || cell === undefined || String(cell).trim() === '')

export interface OpcionesPadron {
  /** Columnas elegidas a mano en la carga; pisan las detectadas por el encabezado. */
  columnas?: ColumnasPadron
  /** Da un ID correlativo (`AUTO-00001`) a quien no trae el suyo. */
  generarCodigo?: boolean
  /** La empresa de quien no trae columna de empresa: la misma para todo el padrón. */
  empresa?: string
}

/** La primera fila con datos: el encabezado, si lo hay. */
export const filaEncabezado = (matrix: unknown[][]): string[] =>
  (matrix.find((r) => !isEmptyRow(r)) ?? []).map((c) => String(c ?? '').trim())

/**
 * Una matriz de celdas (la primera hoja del Excel, o el CSV ya separado) a participantes.
 * Con encabezado reconocible se lee por nombre de columna; sin él, por posición:
 * nombre, correo o departamento, departamento o código, código.
 */
export function parseParticipantMatrix(
  matrix: unknown[][],
  opciones: OpcionesPadron = {},
): ParsedParticipantFile {
  const problems: ParticipantFileProblem[] = []
  const rows: RaffleParticipantInput[] = []
  const firstIdx = matrix.findIndex((r) => !isEmptyRow(r))
  if (firstIdx < 0) return { rows, problems }

  const cols = opciones.columnas ?? mapHeaders(matrix[firstIdx].map((c) => String(c ?? '')))
  const hasHeader =
    opciones.columnas != null ||
    cols.fullName >= 0 ||
    cols.firstName >= 0 ||
    cols.email >= 0 ||
    cols.employeeCode >= 0
  if (hasHeader && cols.fullName < 0 && cols.firstName < 0) {
    problems.push({
      row: firstIdx + 1,
      message: 'Falta la columna "Nombre" (o "Nombre completo").',
    })
    return { rows, problems }
  }

  const seenCodes = new Map<string, number>()
  const seenEmails = new Map<string, number>()
  let siguienteAuto = 0

  for (let i = hasHeader ? firstIdx + 1 : firstIdx; i < matrix.length; i++) {
    const raw = matrix[i] ?? []
    if (isEmptyRow(raw)) continue
    const rowNumber = i + 1
    const cell = (idx: number) => (idx >= 0 ? String(raw[idx] ?? '').trim() : '')

    let fullName: string
    let email = ''
    let department = ''
    let company = ''
    let location = ''
    let firstName = ''
    let lastName = ''
    let employeeCode = ''
    let phone = ''
    let qrCode = ''
    let checkedIn: boolean | undefined

    if (hasHeader) {
      fullName =
        cell(cols.fullName) || [cell(cols.firstName), cell(cols.lastName)].filter(Boolean).join(' ')
      email = cell(cols.email)
      department = cell(cols.department)
      company = cell(cols.company)
      location = cell(cols.location)
      firstName = cell(cols.firstName)
      lastName = cell(cols.lastName)
      employeeCode = cell(cols.employeeCode)
      phone = cell(cols.phone)
      qrCode = cell(cols.qrCode)
      if (cols.checkedIn >= 0) checkedIn = parseBooleanCell(raw[cols.checkedIn])
    } else {
      fullName = cell(0)
      const [c1, c2, c3] = [cell(1), cell(2), cell(3)]
      if (c1.includes('@')) {
        email = c1
        department = c2
        employeeCode = c3
      } else {
        department = c1
        if (c2.includes('@')) email = c2
        else employeeCode = c2
        if (c3 && !employeeCode) employeeCode = c3
      }
    }

    fullName = fullName.replace(/\s+/g, ' ')
    if (!fullName) {
      problems.push({ row: rowNumber, message: 'Fila sin nombre: no se importa.' })
      continue
    }
    if (email && !EMAIL.test(email)) {
      problems.push({
        row: rowNumber,
        message: `Correo inválido "${email}": se importa sin correo.`,
      })
      email = ''
    }

    if (!employeeCode && opciones.generarCodigo) {
      // Un código del archivo puede coincidir con uno generado: se salta al siguiente libre.
      do employeeCode = `AUTO-${String(++siguienteAuto).padStart(5, '0')}`
      while (seenCodes.has(employeeCode))
    }

    const codeKey = employeeCode.toUpperCase()
    if (codeKey) {
      const prev = seenCodes.get(codeKey)
      if (prev !== undefined) {
        problems.push({
          row: rowNumber,
          message: `Código "${employeeCode}" repetido (ya está en la fila ${prev}): no se importa.`,
        })
        continue
      }
      seenCodes.set(codeKey, rowNumber)
    }
    const emailKey = email.toLowerCase()
    if (emailKey) {
      const prev = seenEmails.get(emailKey)
      if (prev !== undefined && !codeKey) {
        problems.push({
          row: rowNumber,
          message: `Correo "${email}" repetido (ya está en la fila ${prev}): no se importa.`,
        })
        continue
      }
      if (prev !== undefined) {
        // Con código distinto es otra persona: entra, pero el correo ya es de la fila anterior.
        problems.push({
          row: rowNumber,
          message: `Correo "${email}" compartido con la fila ${prev}: se importa sin correo.`,
        })
        email = ''
      } else {
        seenEmails.set(emailKey, rowNumber)
      }
    }

    // Sin correo ni ID no hay cómo reconocer a la persona: el servicio la rechaza.
    if (!email && !employeeCode) {
      problems.push({ row: rowNumber, message: 'Fila sin correo ni ID: no se importa.' })
      continue
    }

    rows.push({
      fullName,
      email: email || undefined,
      firstName: firstName || undefined,
      lastName: lastName || undefined,
      // La empresa es la del archivo, o la que la carga pone a todos (Banreservas). La regla de un
      // premio por grupo usa el departamento (RuleGroup del servicio); sin columna de
      // departamento, la ubicación hace de área (así llega el padrón de Banreservas).
      company: company || opciones.empresa?.trim() || undefined,
      department: department || location || undefined,
      location: location || undefined,
      employeeCode: employeeCode || undefined,
      phone: phone || undefined,
      qrCode: qrCode || undefined,
      checkedIn,
    })
  }

  return { rows, problems }
}

/** Una línea de CSV respetando comillas: `"Pérez, Juan";x` son dos celdas. */
export function splitDelimitedLine(line: string, delimiter: string): string[] {
  const cells: string[] = []
  let current = ''
  let quoted = false
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]
    if (quoted) {
      if (ch === '"' && line[i + 1] === '"') {
        current += '"'
        i++
      } else if (ch === '"') {
        quoted = false
      } else {
        current += ch
      }
    } else if (ch === '"' && current.trim() === '') {
      quoted = true
      current = ''
    } else if (ch === delimiter) {
      cells.push(current.trim())
      current = ''
    } else {
      current += ch
    }
  }
  cells.push(current.trim())
  return cells
}

/**
 * CSV o TXT. Con separador (`;`, tabulador, `|` o `,`, el primero que aparezca en la primera
 * línea) se lee como tabla; sin separador, una persona por línea, opcionalmente "Nombre - Depto".
 */
export function parseParticipantText(
  text: string,
  opciones: OpcionesPadron = {},
): ParsedParticipantFile {
  return parseParticipantMatrix(textoAMatriz(text), opciones)
}

/** El texto de un CSV o TXT como matriz de celdas, sin interpretar columnas. */
export function textoAMatriz(text: string): unknown[][] {
  const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/)
  const first = lines.find((l) => l.trim() !== '') ?? ''
  const delimiter = [';', '\t', '|', ','].find((d) => first.includes(d))
  if (delimiter) {
    return lines.map((l) => (l.trim() === '' ? [] : splitDelimitedLine(l, delimiter)))
  }
  return lines.map((line, i) => {
    const trimmed = line.trim()
    if (
      !trimmed ||
      (i === 0 && ['nombre', 'nombres', 'nombre completo'].includes(normalizeHeader(trimmed)))
    )
      return []
    const [name, dept] = trimmed.split(/\s+[-–]\s+/)
    return [name, dept ?? '']
  })
}

export interface IdentificadoresDeArchivo {
  /** De cada fila, su correo si lo tiene y, si no, su ID. */
  valores: string[]
  /** Filas con datos pero sin correo ni ID. */
  descartadas: number
}

/**
 * Los correos o IDs de un archivo, para la exclusión en lote. Con encabezado se leen las columnas
 * de correo e ID del padrón (`correo`, `id`, `codigo`, `cedula`…); sin él, la primera celda de
 * cada fila: un TXT con un valor por línea.
 */
export function identificadoresDeMatriz(matrix: unknown[][]): IdentificadoresDeArchivo {
  const valores: string[] = []
  let descartadas = 0
  const firstIdx = matrix.findIndex((r) => !isEmptyRow(r))
  if (firstIdx < 0) return { valores, descartadas }

  const cols = mapHeaders(matrix[firstIdx].map((c) => String(c ?? '')))
  const conEncabezado = cols.email >= 0 || cols.employeeCode >= 0
  for (let i = conEncabezado ? firstIdx + 1 : firstIdx; i < matrix.length; i++) {
    const raw = matrix[i] ?? []
    if (isEmptyRow(raw)) continue
    const cell = (idx: number) => (idx >= 0 ? String(raw[idx] ?? '').trim() : '')
    const valor = conEncabezado ? cell(cols.email) || cell(cols.employeeCode) : cell(0)
    if (valor) valores.push(valor)
    else descartadas++
  }
  return { valores, descartadas }
}

/** Los problemas como CSV (separado por `;`, el que abre bien Excel en español). */
export function problemsToCsv(problems: ParticipantFileProblem[]): string {
  const esc = (s: string) => `"${s.replace(/"/g, '""')}"`
  return ['Fila;Problema', ...problems.map((p) => `${p.row};${esc(p.message)}`)].join('\r\n')
}
