import { useState } from 'react'
import { Download, FileText, RefreshCw } from 'lucide-react'
import type { NotarialRecordResponse, RaffleResponse } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { formatDateTime } from '@/lib/format'
import { downloadNotarialRecord } from '../raffleView'
import { ReemitirActaModal } from './ReemitirActaModal'

/**
 * Las versiones del acta, de la más nueva a la más vieja. Es el texto que se genera para que el
 * notario lo firme en papel, no el original firmado, que no está en la plataforma (spec raffles/03
 * §4.1).
 */
export function ActaNotarialCard({ eventId, raffle }: { eventId: string; raffle: RaffleResponse }) {
  const canDraw = useCan(ROLES.rafflesDraw)
  const [reemitiendo, setReemitiendo] = useState(false)
  const [descargando, setDescargando] = useState<number | null>(null)
  const [error, setError] = useState<unknown>(null)

  const descargar = async (record: NotarialRecordResponse) => {
    setDescargando(record.version)
    setError(null)
    try {
      await downloadNotarialRecord(record)
    } catch (err) {
      setError(err)
    } finally {
      setDescargando(null)
    }
  }

  return (
    <Card className="p-6 bg-white dark:bg-card border border-slate-200 dark:border-slate-800 space-y-4">
      <div className="flex items-center gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
        <FileText className="w-5 h-5 text-blue-600 dark:text-blue-400" />
        <h3 className="text-base font-bold text-slate-900 dark:text-white">Acta notarial</h3>
      </div>

      <p className="text-xs text-slate-500">
        Acta generada: el texto para imprimir y que el notario y los testigos firmen en papel. El
        original firmado no se guarda en la plataforma.
      </p>

      {raffle.notarialRecords.length === 0 ? (
        <p className="text-xs text-slate-400">Se genera al completar el sorteo.</p>
      ) : (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {raffle.notarialRecords.map((record, i) => (
            <li key={record.version} className="flex items-center justify-between gap-3 py-2">
              <div>
                <span className="text-sm font-bold text-slate-900 dark:text-white">
                  Versión {record.version}
                </span>
                <span className="block text-[11px] text-slate-500">
                  Generada el {formatDateTime(record.generatedAt)}
                  {i === 0 ? ' · vigente' : ''}
                </span>
              </div>
              <button
                type="button"
                disabled={descargando !== null}
                onClick={() => void descargar(record)}
                title={`Descargar el acta generada, versión ${record.version}`}
                className="p-1.5 rounded-lg text-slate-600 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Download className="w-4 h-4" />
              </button>
            </li>
          ))}
        </ul>
      )}

      {error != null && <ErrorNote error={error} />}

      {/* Solo completado: antes, las autoridades se corrigen en su tarjeta */}
      {raffle.state === 'completed' && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={!canDraw.allowed}
          title={
            canDraw.allowed
              ? 'Corregir el notario o los testigos y generar una versión nueva'
              : canDraw.title
          }
          onClick={() => setReemitiendo(true)}
          className="text-xs font-bold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <RefreshCw className="w-3.5 h-3.5 mr-1" />
          Reemitir acta
        </Button>
      )}

      {reemitiendo && (
        <ReemitirActaModal
          eventId={eventId}
          raffle={raffle}
          onClose={() => setReemitiendo(false)}
        />
      )}
    </Card>
  )
}
