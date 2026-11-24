import { useMemo, useState } from 'react'
import * as XLSX from 'xlsx'
import { Download, FileSpreadsheet, FileText, CheckCircle2, AlertTriangle } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { FileDropzone } from '@/components/ui/FileDropzone'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { useImportRaffleParticipants, useRaffle } from '../hooks'
import { useBranding } from '@/lib/branding'
import { sinPuerta } from '../raffleView'
import {
  CAMPOS_PADRON,
  filaEncabezado,
  mapHeaders,
  parseParticipantMatrix,
  problemsToCsv,
  textoAMatriz,
  type CampoPadron,
  type ColumnasPadron,
} from '../participantFile'
import { guardarEtiquetas, leerEtiquetas, type EtiquetasPadron } from '../etiquetasPadron'

/** Los campos cuyo nombre se ve en pantalla y se puede cambiar (Cédula, Sede, Sucursal…). */
const CON_ETIQUETA: readonly CampoPadron[] = ['employeeCode', 'department', 'location', 'company']
const tieneEtiqueta = (c: CampoPadron): c is keyof EtiquetasPadron => CON_ETIQUETA.includes(c)

interface CargarParticipantesModalProps {
  eventId: string
  onClose: () => void
  onImported?: (summary: { imported: number; updated: number; total: number }) => void
}

type FileSource = 'excel' | 'csv' | 'txt'

export function CargarParticipantesModal({
  eventId,
  onClose,
  onImported,
}: CargarParticipantesModalProps) {
  const importMutation = useImportRaffleParticipants(eventId)
  // Sin puerta todos llegan presentes: no se ofrece elegir.
  const { data: raffle } = useRaffle(eventId)
  const todosPresentes = raffle ? sinPuerta(raffle) : false
  const [fileName, setFileName] = useState('')
  const [source, setSource] = useState<FileSource>('excel')
  const [readError, setReadError] = useState<string | null>(null)
  const [matriz, setMatriz] = useState<unknown[][] | null>(null)
  const [encabezados, setEncabezados] = useState<string[]>([])
  // Con encabezado, cada campo apunta a una columna; sin él se lee por posición.
  const [columnas, setColumnas] = useState<ColumnasPadron | null>(null)
  const [etiquetas, setEtiquetas] = useState<EtiquetasPadron>(() => leerEtiquetas(eventId))
  const [generarCodigo, setGenerarCodigo] = useState(false)
  // La empresa es una sola para todo el padrón: la del sorteo (Banreservas), editable.
  const branding = useBranding(eventId)
  const [empresa, setEmpresa] = useState(branding.companyName)
  const [markCheckedIn, setMarkCheckedIn] = useState(true)
  const [replaceExisting, setReplaceExisting] = useState(false)

  const result = useMemo(
    () =>
      matriz
        ? parseParticipantMatrix(matriz, {
            columnas: columnas ?? undefined,
            generarCodigo,
            empresa,
          })
        : null,
    [matriz, columnas, generarCodigo, empresa],
  )
  const parsed = result?.rows ?? []
  const problems = result?.problems ?? []
  const parseError =
    readError ??
    (result && parsed.length === 0
      ? 'No se detectaron filas válidas. Elige la columna del nombre (o del nombre completo).'
      : null)

  const handleFile = async (file: File) => {
    setReadError(null)
    setMatriz(null)
    setFileName(file.name)
    const ext = file.name.split('.').pop()?.toLowerCase() ?? ''

    try {
      let m: unknown[][]
      if (ext === 'txt' || ext === 'csv') {
        setSource(ext)
        m = textoAMatriz(await file.text())
      } else {
        setSource('excel')
        const wb = XLSX.read(await file.arrayBuffer(), { type: 'array' })
        const sheetName = wb.SheetNames[0]
        if (!sheetName) {
          setReadError('El libro de Excel no contiene hojas.')
          return
        }
        m = XLSX.utils.sheet_to_json<unknown[]>(wb.Sheets[sheetName], { header: 1, raw: false })
      }
      const cabecera = filaEncabezado(m)
      const detectadas = mapHeaders(cabecera)
      const conEncabezado =
        detectadas.fullName >= 0 ||
        detectadas.firstName >= 0 ||
        detectadas.email >= 0 ||
        detectadas.employeeCode >= 0
      setEncabezados(conEncabezado ? cabecera : [])
      setColumnas(conEncabezado ? detectadas : null)
      // El nombre en pantalla de cada dato es, de entrada, el encabezado del archivo: "Cédula".
      if (conEncabezado) {
        setEtiquetas((prev) => {
          const next = { ...prev }
          for (const c of CON_ETIQUETA) {
            if (tieneEtiqueta(c) && detectadas[c] >= 0 && cabecera[detectadas[c]]) {
              next[c] = cabecera[detectadas[c]]
            }
          }
          return next
        })
      }
      setMatriz(m)
    } catch (err) {
      setReadError(err instanceof Error ? err.message : 'No se pudo leer el archivo seleccionado.')
    }
  }

  const elegirColumna = (campo: CampoPadron, idx: number) => {
    if (!columnas) return
    // Una columna sirve a un solo campo: si otro la tenía, la suelta.
    const next = { ...columnas }
    for (const c of Object.keys(next) as CampoPadron[]) if (next[c] === idx) next[c] = -1
    next[campo] = idx
    setColumnas(next)
    if (tieneEtiqueta(campo) && idx >= 0 && encabezados[idx]) {
      setEtiquetas((prev) => ({ ...prev, [campo]: encabezados[idx] }))
    }
  }

  const downloadProblems = () => {
    const blob = new Blob(['\uFEFF' + problemsToCsv(problems)], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `errores_${fileName.replace(/\.[^.]+$/, '') || 'padron'}.csv`
    link.click()
    URL.revokeObjectURL(url)
  }

  const downloadSample = (format: 'xlsx' | 'csv' | 'txt') => {
    if (format === 'xlsx') {
      const ws = XLSX.utils.json_to_sheet([
        {
          Nombre: 'Juan Pérez',
          Correo: 'juan.perez@empresa.com',
          Departamento: 'Tecnología',
          Codigo: 'EMP-101',
          Telefono: '809-555-0101',
        },
        {
          Nombre: 'María Gómez',
          Correo: 'maria.gomez@empresa.com',
          Departamento: 'Finanzas',
          Codigo: 'EMP-102',
          Telefono: '809-555-0102',
        },
        {
          Nombre: 'Luis Martínez',
          Correo: 'luis.martinez@empresa.com',
          Departamento: 'Operaciones',
          Codigo: 'EMP-103',
          Telefono: '809-555-0103',
        },
      ])
      const wb = XLSX.utils.book_new()
      XLSX.utils.book_append_sheet(wb, ws, 'Participantes')
      XLSX.writeFile(wb, 'plantilla_participantes_rifa.xlsx')
      return
    }
    const content =
      format === 'csv'
        ? 'Nombre,Correo,Departamento,Codigo,Telefono\nJuan Pérez,juan.perez@empresa.com,Tecnología,EMP-101,809-555-0101\nMaría Gómez,maria.gomez@empresa.com,Finanzas,EMP-102,809-555-0102\nLuis Martínez,luis.martinez@empresa.com,Operaciones,EMP-103,809-555-0103\n'
        : 'Nombre|Departamento|Codigo|Correo\nJuan Pérez|Tecnología|EMP-101|juan.perez@empresa.com\nMaría Gómez|Finanzas|EMP-102|maria.gomez@empresa.com\nLuis Martínez|Operaciones|EMP-103|luis.martinez@empresa.com\n'
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `plantilla_participantes_rifa.${format}`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleConfirm = () => {
    if (parsed.length === 0) return
    guardarEtiquetas(eventId, etiquetas)
    importMutation.mutate(
      {
        participants: parsed,
        markCheckedIn: todosPresentes || markCheckedIn,
        replaceExisting,
        source,
      },
      {
        onSuccess: (res) => {
          onImported?.(res)
          onClose()
        },
      },
    )
  }

  return (
    <Modal title="Cargar Participantes (Excel, CSV o TXT)" onClose={onClose} wide>
      <div className="space-y-4 text-xs">
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-2xl border border-slate-200 bg-slate-50/80 p-3.5 dark:border-slate-800 dark:bg-slate-800/50">
          <div>
            <p className="font-bold text-slate-800 dark:text-slate-100">
              Formatos compatibles: <code>.xlsx</code>, <code>.xls</code>, <code>.csv</code>,{' '}
              <code>.txt</code>
            </p>
            <p className="text-[11px] text-slate-500">
              Las columnas se detectan solas (Nombre, ID o Cédula, Departamento o Sede, Correo…) y
              se pueden corregir y renombrar después de elegir el archivo.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => downloadSample('xlsx')}
              className="cursor-pointer text-[11px]"
            >
              <Download size={12} className="mr-1" /> Plantilla Excel
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => downloadSample('csv')}
              className="cursor-pointer text-[11px]"
            >
              <Download size={12} className="mr-1" /> CSV
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => downloadSample('txt')}
              className="cursor-pointer text-[11px]"
            >
              <Download size={12} className="mr-1" /> TXT
            </Button>
          </div>
        </div>

        <FileDropzone
          accept=".xlsx,.xls,.csv,.txt"
          onFile={handleFile}
          title={fileName || 'Excel, CSV o TXT con el padrón'}
          hint="Un TXT puede traer un nombre por línea"
        />

        {parseError && (
          <div className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-rose-700 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-300">
            <AlertTriangle size={15} className="mt-0.5 shrink-0" />
            <span>{parseError}</span>
          </div>
        )}

        {columnas && encabezados.length > 0 && (
          <div className="space-y-2 rounded-xl border border-slate-200 p-3 dark:border-slate-800">
            <p className="font-bold text-slate-800 dark:text-slate-100">Columnas del archivo</p>
            <p className="text-[11px] text-slate-500">
              Elige qué columna es cada dato y cómo se llama en pantalla: el ID puede ser la
              «Cédula» y el departamento la «Sede» o la «Sucursal».
            </p>
            <div className="grid grid-cols-[minmax(0,8rem)_1fr_1fr] items-center gap-x-2 gap-y-1.5">
              <span className="text-[10px] font-bold uppercase text-slate-500">Dato</span>
              <span className="text-[10px] font-bold uppercase text-slate-500">Columna</span>
              <span className="text-[10px] font-bold uppercase text-slate-500">
                Nombre en pantalla
              </span>
              {CAMPOS_PADRON.map(({ campo, etiqueta }) => (
                <div key={campo} className="contents">
                  <span className="font-semibold text-slate-700 dark:text-slate-200">
                    {etiqueta}
                  </span>
                  <select
                    aria-label={`Columna de ${etiqueta}`}
                    value={columnas[campo]}
                    onChange={(e) => elegirColumna(campo, Number(e.target.value))}
                    className="rounded-lg border border-slate-200 bg-white px-2 py-1 dark:border-slate-700 dark:bg-slate-900"
                  >
                    <option value={-1}>(ninguna)</option>
                    {encabezados.map((h, i) => (
                      <option key={i} value={i}>
                        {h || `Columna ${i + 1}`}
                      </option>
                    ))}
                  </select>
                  {tieneEtiqueta(campo) ? (
                    <input
                      aria-label={`Nombre en pantalla de ${etiqueta}`}
                      value={etiquetas[campo]}
                      maxLength={40}
                      onChange={(e) =>
                        setEtiquetas((prev) => ({ ...prev, [campo]: e.target.value }))
                      }
                      className="rounded-lg border border-slate-200 bg-white px-2 py-1 dark:border-slate-700 dark:bg-slate-900"
                    />
                  ) : (
                    <span />
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {matriz && (
          <label className="flex flex-wrap items-center gap-2 font-semibold text-slate-700 dark:text-slate-200">
            <span>Empresa de todos los participantes</span>
            <input
              value={empresa}
              onChange={(e) => setEmpresa(e.target.value)}
              maxLength={120}
              className="min-w-56 flex-1 rounded-lg border border-slate-200 bg-white px-2 py-1 font-normal dark:border-slate-700 dark:bg-slate-900"
            />
          </label>
        )}

        {matriz && (
          <label className="flex items-center gap-2 font-semibold text-slate-700 dark:text-slate-200 cursor-pointer">
            <input
              type="checkbox"
              checked={generarCodigo}
              onChange={(e) => setGenerarCodigo(e.target.checked)}
              className="h-4 w-4 rounded border-slate-300"
            />
            <span>
              Generar {etiquetas.employeeCode} automático (AUTO-00001…) a quien no lo traiga
            </span>
          </label>
        )}

        {problems.length > 0 && (
          <div className="space-y-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="flex items-center gap-2 font-bold">
                <AlertTriangle size={15} className="shrink-0" />
                {problems.length} fila(s) con observaciones
              </span>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={downloadProblems}
                className="cursor-pointer"
              >
                <Download size={13} className="mr-1" /> Descargar errores (CSV)
              </Button>
            </div>
            <ul className="max-h-28 space-y-0.5 overflow-auto text-[11px]">
              {problems.slice(0, 20).map((p, idx) => (
                <li key={idx}>
                  <span className="font-bold tabular-nums">Fila {p.row}:</span> {p.message}
                </li>
              ))}
              {problems.length > 20 && <li>… y {problems.length - 20} más en el CSV.</li>}
            </ul>
          </div>
        )}

        {parsed.length > 0 && (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2 font-bold text-emerald-700 dark:text-emerald-400">
                <CheckCircle2 size={16} />
                <span>{parsed.length} participante(s) listos para importar</span>
                <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-bold uppercase text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                  {source === 'excel' ? <FileSpreadsheet size={11} /> : <FileText size={11} />}
                  {source}
                </span>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 rounded-xl border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-800/40">
              {!todosPresentes && (
                <label className="flex items-center gap-2 font-semibold text-slate-700 dark:text-slate-200 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={markCheckedIn}
                    onChange={(e) => setMarkCheckedIn(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-indigo-600"
                  />
                  <span>Marcar con check-in inmediato (habilitados para la ruleta)</span>
                </label>
              )}

              <label className="flex items-center gap-2 font-semibold text-slate-700 dark:text-slate-200 cursor-pointer">
                <input
                  type="checkbox"
                  checked={replaceExisting}
                  onChange={(e) => setReplaceExisting(e.target.checked)}
                  className="h-4 w-4 rounded border-slate-300 text-rose-600"
                />
                <span>Reemplazar padrón actual (en lugar de agregar/actualizar)</span>
              </label>
            </div>

            <div className="max-h-56 overflow-auto rounded-xl border border-slate-200 dark:border-slate-800">
              <table className="w-full text-left text-[11px]">
                <thead className="sticky top-0 bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                  <tr>
                    <th className="px-3 py-2 font-bold">#</th>
                    <th className="px-3 py-2 font-bold">Nombre</th>
                    <th className="px-3 py-2 font-bold">{etiquetas.department}</th>
                    <th className="px-3 py-2 font-bold">{etiquetas.employeeCode}</th>
                    <th className="px-3 py-2 font-bold">Correo</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {parsed.slice(0, 50).map((row, idx) => (
                    <tr key={idx} className="hover:bg-slate-50 dark:hover:bg-slate-800/40">
                      <td className="px-3 py-1.5 text-slate-400 tabular-nums">{idx + 1}</td>
                      <td className="px-3 py-1.5 font-semibold text-slate-900 dark:text-white">
                        {row.fullName}
                      </td>
                      <td className="px-3 py-1.5 text-slate-600 dark:text-slate-300">
                        {row.department || '—'}
                      </td>
                      <td className="px-3 py-1.5 font-mono text-slate-500">
                        {row.employeeCode || 'Auto QR'}
                      </td>
                      <td className="px-3 py-1.5 text-slate-500">{row.email || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {parsed.length > 50 && (
                <p className="border-t border-slate-100 bg-slate-50 px-3 py-1.5 text-center text-[10.5px] text-slate-500 dark:border-slate-800 dark:bg-slate-900">
                  Mostrando primeros 50 de {parsed.length} registros. Todos serán importados.
                </p>
              )}
            </div>
          </div>
        )}

        {importMutation.error != null && <ErrorNote error={importMutation.error} />}

        <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
            Cancelar
          </Button>
          <Button
            type="button"
            onClick={handleConfirm}
            disabled={parsed.length === 0 || importMutation.isPending}
            className="bg-indigo-600 hover:bg-indigo-700 text-white font-bold cursor-pointer"
          >
            {importMutation.isPending
              ? 'Importando...'
              : `Importar ${parsed.length > 0 ? `${parsed.length} participante(s)` : ''}`}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
