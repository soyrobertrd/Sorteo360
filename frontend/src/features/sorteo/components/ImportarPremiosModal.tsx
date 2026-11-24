import { useId, useMemo, useState } from 'react'
import * as XLSX from 'xlsx'
import { AlertTriangle, Upload } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { FileDropzone } from '@/components/ui/FileDropzone'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Select } from '@/components/ui/Field'
import type { RaffleRoundResponse } from '@/api/types'
import { cn } from '@/lib/utils'
import { useImportPrizes } from '../hooks'
import {
  detectarAsignacion,
  leerPremios,
  limpiarMatriz,
  textoAMatriz,
  type AsignacionColumnas,
  type CampoPremio,
} from '../premioFile'

const CAMPOS: { campo: CampoPremio; etiqueta: string; clase: string }[] = [
  {
    campo: 'ronda',
    etiqueta: 'Ronda',
    clase: 'bg-brand/10 text-brand-deep dark:text-brand-accent',
  },
  {
    campo: 'premio',
    etiqueta: 'Premio',
    clase: 'bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300',
  },
  {
    campo: 'valor',
    etiqueta: 'Valor (RD$)',
    clase: 'bg-amber-50 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  },
  {
    campo: 'cantidad',
    etiqueta: 'Cantidad',
    clase: 'bg-violet-50 text-violet-800 dark:bg-violet-950/40 dark:text-violet-300',
  },
  {
    campo: 'patrocinador',
    etiqueta: 'Patrocinador',
    clase: 'bg-sky-50 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300',
  },
]

const FILAS_VISTA_PREVIA = 6
/** El tope de una importación en el servicio (A30): más se rechaza entero. */
const MAX_PREMIOS = 10_000
const letra = (i: number) => XLSX.utils.encode_col(i)

interface ImportarPremiosModalProps {
  eventId: string
  rondas: RaffleRoundResponse[]
  onClose: () => void
  onImported: (cantidad: number) => void
}

/**
 * Premios desde Excel o CSV: Ronda (número), Premio (texto) y Valor (pesos). Se ve el principio del
 * archivo y cada columna se asigna al campo que le toca; las rondas que falten se crean.
 */
export function ImportarPremiosModal({
  eventId,
  rondas,
  onClose,
  onImported,
}: ImportarPremiosModalProps) {
  const importar = useImportPrizes(eventId)
  const captionId = useId()
  const [archivo, setArchivo] = useState('')
  const [matriz, setMatriz] = useState<string[][]>([])
  const [asignacion, setAsignacion] = useState<AsignacionColumnas>({
    ronda: null,
    premio: null,
    valor: null,
    cantidad: null,
    patrocinador: null,
  })
  const [conEncabezado, setConEncabezado] = useState(true)
  const [errorLectura, setErrorLectura] = useState<string | null>(null)

  const leerArchivo = async (file: File) => {
    setErrorLectura(null)
    setArchivo(file.name)
    try {
      const ext = file.name.split('.').pop()?.toLowerCase() ?? ''
      let m: string[][]
      if (ext === 'csv' || ext === 'txt') {
        m = textoAMatriz(await file.text())
      } else {
        const wb = XLSX.read(await file.arrayBuffer(), { type: 'array' })
        const hoja = wb.SheetNames[0]
        m = hoja
          ? limpiarMatriz(
              XLSX.utils.sheet_to_json<unknown[]>(wb.Sheets[hoja], { header: 1, raw: false }),
            )
          : []
      }
      if (m.length === 0) throw new Error('El archivo no tiene filas con datos.')
      const detectado = detectarAsignacion(m)
      setMatriz(m)
      setAsignacion(detectado.asignacion)
      setConEncabezado(detectado.conEncabezado)
    } catch (err) {
      setMatriz([])
      setErrorLectura(err instanceof Error ? err.message : 'No se pudo leer el archivo.')
    }
  }

  /** Una columna alimenta un solo campo: asignarla quita el campo que tuviera antes. */
  const asignarColumna = (columna: number, campo: CampoPremio | '') => {
    setAsignacion((prev) => {
      const next = { ...prev }
      for (const c of Object.keys(next) as CampoPremio[]) if (next[c] === columna) next[c] = null
      if (campo) next[campo] = columna
      return next
    })
  }

  const columnas = Math.max(0, ...matriz.map((f) => f.length))
  const campoDe = (columna: number) => CAMPOS.find((c) => asignacion[c.campo] === columna)
  const { premios, problemas } = useMemo(
    () => leerPremios(matriz, asignacion, conEncabezado),
    [matriz, asignacion, conEncabezado],
  )
  const total = premios.reduce((n, p) => n + p.cantidad, 0)
  const mayorRonda = Math.max(0, ...rondas.map((r) => r.number))
  const rondasNuevas = Math.max(0, Math.max(0, ...premios.map((p) => p.ronda)) - mayorRonda)

  const confirmar = () =>
    importar.mutate({ rounds: rondas, prizes: premios }, { onSuccess: (n) => onImported(n) })

  return (
    <Modal title="Importar premios" onClose={onClose} wide>
      <div className="space-y-4 text-sm">
        <p className="text-slate-600 dark:text-slate-400">
          Un Excel o CSV con <strong>Ronda</strong> (número), <strong>Premio</strong> (texto),{' '}
          <strong>Valor</strong> en pesos, <strong>Cantidad</strong> y, si lo hay,{' '}
          <strong>Patrocinador</strong>: una fila "Ronda 1 · Bono · 10,000 · 25" son 25 bonos. Sin
          cantidad, cada fila es un premio. Si las columnas vienen en otro orden o con otro nombre,
          asígnalas abajo.
        </p>

        <FileDropzone
          accept=".xlsx,.xls,.csv,.txt"
          onFile={leerArchivo}
          title={archivo || 'Excel o CSV con los premios'}
          hint="Se lee la primera hoja del libro"
        />

        {errorLectura && <ErrorNote error={new Error(errorLectura)} />}

        {matriz.length > 0 && (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <label className="flex items-center gap-2 text-slate-700 dark:text-slate-300">
                <input
                  type="checkbox"
                  checked={conEncabezado}
                  onChange={(e) => setConEncabezado(e.target.checked)}
                />
                La primera fila es el encabezado
              </label>
              <span className="text-xs text-slate-500">
                Toca una celda o usa el selector de la columna para asignarla.
              </span>
            </div>

            <div
              tabIndex={0}
              role="region"
              aria-labelledby={captionId}
              className="max-h-80 overflow-auto rounded-xl border border-slate-200 dark:border-slate-700"
            >
              <table className="w-full border-separate border-spacing-0 text-left text-xs">
                <caption id={captionId} className="sr-only">
                  Primeras filas del archivo y el campo asignado a cada columna
                </caption>
                <thead className="sticky top-0 z-10 bg-card">
                  <tr>
                    <th
                      scope="col"
                      className="w-10 border-b border-slate-200 px-2 py-2 dark:border-slate-700"
                    >
                      <span className="sr-only">Fila</span>
                    </th>
                    {Array.from({ length: columnas }, (_, i) => (
                      <th
                        key={i}
                        scope="col"
                        className="min-w-36 border-b border-slate-200 px-2 py-2 font-semibold dark:border-slate-700"
                      >
                        <span className="mb-1 block text-[10px] uppercase text-slate-500">
                          Columna {letra(i)}
                        </span>
                        <Select
                          id={`${captionId}-col-${i}`}
                          aria-label={`Campo de la columna ${letra(i)}`}
                          value={campoDe(i)?.campo ?? ''}
                          onChange={(e) => asignarColumna(i, e.target.value as CampoPremio | '')}
                          className="h-8 py-0 text-xs"
                        >
                          <option value="">— No importar —</option>
                          {CAMPOS.map((c) => (
                            <option key={c.campo} value={c.campo}>
                              {c.etiqueta}
                            </option>
                          ))}
                        </Select>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {matriz.slice(0, FILAS_VISTA_PREVIA).map((fila, f) => {
                    const esEncabezado = conEncabezado && f === 0
                    return (
                      <tr key={f} className={cn(esEncabezado && 'font-semibold')}>
                        <th
                          scope="row"
                          className="border-b border-slate-100 px-2 py-1.5 text-right font-normal text-slate-400 dark:border-slate-800"
                        >
                          {f + 1}
                        </th>
                        {Array.from({ length: columnas }, (_, i) => (
                          <td
                            key={i}
                            onClick={() =>
                              document.getElementById(`${captionId}-col-${i}`)?.focus()
                            }
                            className={cn(
                              'max-w-56 cursor-pointer truncate border-b border-slate-100 px-2 py-1.5 dark:border-slate-800',
                              !esEncabezado && campoDe(i)?.clase,
                              esEncabezado && 'bg-muted',
                            )}
                            title={fila[i]}
                          >
                            {fila[i] ?? ''}
                          </td>
                        ))}
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
            {matriz.length > FILAS_VISTA_PREVIA && (
              <p className="text-xs text-slate-500">
                Vista previa de {FILAS_VISTA_PREVIA} de {matriz.length} filas.
              </p>
            )}

            {asignacion.premio == null ? (
              <p className="flex items-center gap-2 text-amber-700 dark:text-amber-300">
                <AlertTriangle size={15} /> Asigna la columna del premio para continuar.
              </p>
            ) : (
              <p className="rounded-xl bg-muted px-3 py-2 text-slate-700 dark:text-slate-200">
                Se importarán <strong>{total.toLocaleString('es-DO')}</strong> premio(s) en{' '}
                {premios.length} línea(s)
                {asignacion.ronda == null && ' en la ronda 1'}
                {rondasNuevas > 0 && (
                  <>
                    {' '}
                    y se crearán <strong>{rondasNuevas}</strong> ronda(s) nueva(s)
                  </>
                )}
                .
              </p>
            )}

            {asignacion.premio != null && problemas.length > 0 && (
              <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
                <p className="mb-1 font-bold">{problemas.length} fila(s) no se importarán:</p>
                <ul className="list-disc space-y-0.5 pl-5 text-xs">
                  {problemas.slice(0, 5).map((p) => (
                    <li key={p.fila}>
                      Fila {p.fila}: {p.mensaje}
                    </li>
                  ))}
                  {problemas.length > 5 && <li>y {problemas.length - 5} más.</li>}
                </ul>
              </div>
            )}
          </>
        )}

        {total > MAX_PREMIOS && (
          <p className="flex items-center gap-2 text-amber-700 dark:text-amber-300">
            <AlertTriangle size={15} /> Se importan hasta {MAX_PREMIOS.toLocaleString('es-DO')}{' '}
            premios por vez: divide el archivo.
          </p>
        )}
        {importar.error != null && <ErrorNote error={importar.error} />}

        <div className="flex justify-end gap-2 border-t border-slate-100 pt-4 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            type="button"
            onClick={confirmar}
            disabled={
              importar.isPending || total === 0 || total > MAX_PREMIOS || asignacion.premio == null
            }
          >
            <Upload className="mr-1 h-4 w-4" />
            {importar.isPending ? 'Importando…' : `Importar ${total} premio(s)`}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
