import { useMemo, useState } from 'react'
import * as XLSX from 'xlsx'
import { toast } from 'sonner'
import { AlertTriangle, CheckCircle2 } from 'lucide-react'
import type { BulkExcludeAttendeesRequest, BulkExcludeAttendeesResponse } from '@/api/types'
import { Modal } from '@/components/ui/Modal'
import { FileDropzone } from '@/components/ui/FileDropzone'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorNote } from '@/components/ui/Feedback'
import { Tabs } from '@/components/ui/Tabs'
import { formatDate } from '@/lib/format'
import { useBulkExcludeAttendees, useRaffleList } from '../hooks'
import {
  identificadoresDeMatriz,
  textoAMatriz,
  type IdentificadoresDeArchivo,
} from '../participantFile'

type Fuente = 'sorteo' | 'archivo'

/**
 * Saca del bombo, de una vez, a los ganadores de otro sorteo o a una lista de correos e IDs. Cada
 * persona se puede volver a meter después con «Quitar ajuste manual» en la tabla.
 */
export function ExcluirEnLoteModal({ eventId, onClose }: { eventId: string; onClose: () => void }) {
  const excluir = useBulkExcludeAttendees(eventId)
  const { data: sorteos } = useRaffleList()
  const [fuente, setFuente] = useState<Fuente>('sorteo')
  const [sourceEventId, setSourceEventId] = useState('')
  const [archivo, setArchivo] = useState<{
    nombre: string
    leido: IdentificadoresDeArchivo
  } | null>(null)
  const [errorLectura, setErrorLectura] = useState<string | null>(null)
  const [nota, setNota] = useState('')
  const [confirmar, setConfirmar] = useState(false)
  const [resultado, setResultado] = useState<BulkExcludeAttendeesResponse | null>(null)

  const otros = useMemo(
    () => (sorteos?.items ?? []).filter((s) => s.eventId !== eventId),
    [sorteos, eventId],
  )
  const elegido = otros.find((s) => s.eventId === sourceEventId)

  const leerArchivo = async (file: File) => {
    setErrorLectura(null)
    setArchivo(null)
    try {
      const ext = file.name.split('.').pop()?.toLowerCase() ?? ''
      let matriz: unknown[][]
      if (ext === 'txt' || ext === 'csv') {
        matriz = textoAMatriz(await file.text())
      } else {
        const wb = XLSX.read(await file.arrayBuffer(), { type: 'array' })
        const hoja = wb.SheetNames[0]
        if (!hoja) {
          setErrorLectura('El libro de Excel no contiene hojas.')
          return
        }
        matriz = XLSX.utils.sheet_to_json<unknown[]>(wb.Sheets[hoja], { header: 1, raw: false })
      }
      setArchivo({ nombre: file.name, leido: identificadoresDeMatriz(matriz) })
    } catch (err) {
      setErrorLectura(
        err instanceof Error ? err.message : 'No se pudo leer el archivo seleccionado.',
      )
    }
  }

  const listo = fuente === 'sorteo' ? Boolean(elegido) : (archivo?.leido.valores.length ?? 0) > 0

  const enviar = () => {
    const body: BulkExcludeAttendeesRequest = {
      sourceEventId: fuente === 'sorteo' ? sourceEventId : null,
      identifiers: fuente === 'archivo' ? (archivo?.leido.valores ?? []) : null,
      note: nota.trim() || null,
    }
    excluir.mutate(body, {
      onSuccess: (res) => {
        setConfirmar(false)
        setResultado(res)
        toast.success(`${res.excluded} excluidos · ${res.alreadyExcluded} ya estaban`)
      },
    })
  }

  const sinEncontrar = resultado
    ? resultado.notInRoster.length + resultado.invalid.length + resultado.unidentifiable.length
    : 0

  return (
    <Modal title="Excluir en lote" onClose={onClose} wide>
      <div className="space-y-4 text-xs">
        <Tabs<Fuente>
          variant="segmented"
          size="sm"
          value={fuente}
          onChange={(f) => {
            setFuente(f)
            setResultado(null)
          }}
          items={[
            { key: 'sorteo', label: 'Ganadores de otro sorteo' },
            { key: 'archivo', label: 'Importar archivo' },
          ]}
        />

        {fuente === 'sorteo' ? (
          <label className="block space-y-1 font-semibold text-slate-700 dark:text-slate-200">
            <span>Sorteo de origen</span>
            <select
              aria-label="Sorteo de origen"
              value={sourceEventId}
              onChange={(e) => setSourceEventId(e.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white px-2 py-1.5 font-normal dark:border-slate-700 dark:bg-slate-900"
            >
              <option value="">Elija un sorteo…</option>
              {otros.map((s) => (
                <option key={s.eventId} value={s.eventId}>
                  {s.eventName} · {formatDate(s.eventStartDate)} · {s.drawnCount} ganador(es)
                </option>
              ))}
            </select>
            <span className="block font-normal text-slate-500">
              Quedan fuera sus ganadores vivos; un ganador anulado no cuenta.
            </span>
          </label>
        ) : (
          <div className="space-y-2">
            <FileDropzone
              accept=".xlsx,.xls,.csv,.txt"
              onFile={leerArchivo}
              title={archivo?.nombre || 'Excel, CSV o TXT con correos o IDs'}
              hint="Columnas Correo o ID (Código, Cédula…), o un valor por línea"
            />
            {errorLectura && <ErrorNote error={new Error(errorLectura)} />}
            {archivo && (
              <div className="space-y-1 rounded-xl border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-800/40">
                <p className="flex items-center gap-2 font-bold text-emerald-700 dark:text-emerald-400">
                  <CheckCircle2 size={15} />
                  {archivo.leido.valores.length} persona(s) para excluir
                </p>
                {archivo.leido.descartadas > 0 && (
                  <p className="text-amber-700 dark:text-amber-300">
                    {archivo.leido.descartadas} fila(s) sin correo ni ID: no se envían.
                  </p>
                )}
                <ul className="max-h-24 overflow-auto font-mono text-[11px] text-slate-600 dark:text-slate-300">
                  {archivo.leido.valores.slice(0, 8).map((v, i) => (
                    <li key={i}>{v}</li>
                  ))}
                  {archivo.leido.valores.length > 8 && (
                    <li>… y {archivo.leido.valores.length - 8} más</li>
                  )}
                </ul>
              </div>
            )}
          </div>
        )}

        <label className="block space-y-1 font-semibold text-slate-700 dark:text-slate-200">
          <span>Nota (opcional)</span>
          <input
            value={nota}
            onChange={(e) => setNota(e.target.value)}
            maxLength={1000}
            placeholder="Si se deja vacía, se anota de dónde salió la exclusión"
            className="w-full rounded-lg border border-slate-200 bg-white px-2 py-1.5 font-normal dark:border-slate-700 dark:bg-slate-900"
          />
        </label>

        {resultado && (
          <div className="space-y-2 rounded-xl border border-slate-200 p-3 dark:border-slate-800">
            <p className="font-bold text-slate-800 dark:text-slate-100">
              {resultado.excluded} excluidos · {resultado.alreadyExcluded} ya estaban
            </p>
            {sinEncontrar > 0 && (
              <details className="text-amber-800 dark:text-amber-200">
                <summary className="flex cursor-pointer items-center gap-2 font-semibold">
                  <AlertTriangle size={14} /> {sinEncontrar} sin excluir
                </summary>
                <ListaNoEncontrados
                  titulo="No están en el padrón o en la puerta"
                  valores={resultado.notInRoster}
                />
                <ListaNoEncontrados
                  titulo="No son un correo ni el ID de nadie"
                  valores={resultado.invalid}
                />
                <ListaNoEncontrados
                  titulo="Sin correo ni ID: exclúyelos a mano en la tabla"
                  valores={resultado.unidentifiable}
                />
              </details>
            )}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onClose}>
            {resultado ? 'Cerrar' : 'Cancelar'}
          </Button>
          <Button
            type="button"
            disabled={!listo || excluir.isPending}
            onClick={() => setConfirmar(true)}
          >
            Excluir
          </Button>
        </div>
      </div>

      {confirmar && (
        <ConfirmDialog
          title="Excluir en lote"
          confirmLabel="Excluir"
          destructive
          pending={excluir.isPending}
          error={excluir.error}
          onConfirm={enviar}
          onCancel={() => setConfirmar(false)}
        >
          <p>
            {fuente === 'sorteo' ? (
              <>
                Se sacan del bombo los ganadores vivos de <strong>{elegido?.eventName}</strong>.
              </>
            ) : (
              <>
                Se sacan del bombo hasta <strong>{archivo?.leido.valores.length}</strong> persona(s)
                del archivo.
              </>
            )}{' '}
            Cada una se puede volver a meter desde la tabla.
          </p>
        </ConfirmDialog>
      )}
    </Modal>
  )
}

function ListaNoEncontrados({ titulo, valores }: { titulo: string; valores: string[] }) {
  if (valores.length === 0) return null
  return (
    <div className="mt-2">
      <p className="font-semibold">
        {titulo} ({valores.length})
      </p>
      <ul className="max-h-24 overflow-auto font-mono text-[11px]">
        {valores.map((v, i) => (
          <li key={i}>{v}</li>
        ))}
      </ul>
    </div>
  )
}
