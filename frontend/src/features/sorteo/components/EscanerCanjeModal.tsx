import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Search, CheckCircle2, Clock, Ban } from 'lucide-react'
import type { PrizeDrawLookupResponse, RaffleResponse } from '@/api/types'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import { formatDateTime } from '@/lib/format'
import { prizeDrawStatusLabel, prizeDrawVoidCodeLabel } from '@/lib/labels'
import { usePrizeDrawByCode } from '../hooks'
import { liveDraw, prizesOf } from '../raffleView'

interface EscanerCanjeModalProps {
  raffle: RaffleResponse
  /** Abre la entrega de ese premio de este sorteo. */
  onEntregar: (prizeId: string) => void
  onClose: () => void
}

/**
 * El mostrador: busca un código de canje en todos los sorteos, o un ganador por nombre en este. Nunca
 * entrega: un código pendiente lleva a la entrega del premio, con su firma (spec raffles/03 §2.4). Un
 * código anulado se dice anulado, no inexistente.
 */
export function EscanerCanjeModal({ raffle, onEntregar, onClose }: EscanerCanjeModalProps) {
  const [codigo, setCodigo] = useState('')
  const [buscado, setBuscado] = useState('')
  const [nombre, setNombre] = useState('')
  const lookup = usePrizeDrawByCode(buscado)

  const handleBuscar = (e: FormEvent) => {
    e.preventDefault()
    setBuscado(codigo.trim())
  }

  // La búsqueda por nombre es de la pantalla del sorteo, sobre sus ganadores sin entregar
  const filtro = nombre.trim().toLowerCase()
  const pendientes = prizesOf(raffle).flatMap((premio) => {
    const draw = liveDraw(premio)
    return draw && premio.state === 'drawn' && draw.winnerName.toLowerCase().includes(filtro)
      ? [{ premio, draw }]
      : []
  })

  return (
    <Modal title="Validación y Canje de Premios con Código QR" onClose={onClose}>
      <div className="space-y-5">
        <form onSubmit={handleBuscar}>
          <Field label="Escanear o escribir el código de canje">
            <div className="flex gap-2">
              <div className="flex-1">
                <TextInput
                  value={codigo}
                  onChange={(e) => setCodigo(e.target.value)}
                  placeholder="Ej: RD-7K3M-Q9XA"
                />
              </div>
              <Button
                type="submit"
                className="bg-blue-600 hover:bg-blue-700 text-white font-bold cursor-pointer"
              >
                <Search className="w-4 h-4 mr-1.5" />
                Buscar
              </Button>
            </div>
          </Field>
        </form>

        {buscado &&
          (lookup.isLoading ? (
            <Spinner />
          ) : lookup.error != null ? (
            <ErrorNote error={lookup.error} />
          ) : (
            lookup.data && (
              <ResultadoCodigo
                resultado={lookup.data}
                deEsteSorteo={lookup.data.eventId === raffle.eventId}
                onEntregar={onEntregar}
                onClose={onClose}
              />
            )
          ))}

        <div className="space-y-2 pt-2">
          <span className="text-xs font-bold text-slate-700 dark:text-slate-300 block">
            Premios Pendientes de Retiro en Sala ({pendientes.length})
          </span>
          <TextInput
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Buscar por nombre del ganador"
          />

          <div className="max-h-40 overflow-y-auto space-y-1.5 pr-1">
            {pendientes.length === 0 ? (
              <p className="text-xs text-slate-500 italic">No hay premios pendientes de entrega.</p>
            ) : (
              pendientes.map(({ premio, draw }) => (
                <button
                  key={premio.id}
                  type="button"
                  onClick={() => onEntregar(premio.id)}
                  className="w-full text-left p-2.5 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:border-blue-500 flex items-center justify-between transition-colors cursor-pointer"
                >
                  <div className="min-w-0 pr-2">
                    <span className="text-xs font-bold text-slate-900 dark:text-white truncate block">
                      {premio.name}
                    </span>
                    <span className="text-[11px] text-slate-500 dark:text-slate-400">
                      {draw.winnerName} • {draw.winnerCompany ?? 'Invitado'}
                    </span>
                  </div>
                  <span className="font-mono text-[10px] font-bold bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 px-2 py-0.5 rounded border border-blue-200 dark:border-blue-800 shrink-0">
                    {draw.redemptionCode}
                  </span>
                </button>
              ))
            )}
          </div>
        </div>

        <div className="flex justify-end pt-3 border-t border-slate-200 dark:border-slate-700">
          <Button type="button" variant="outline" onClick={onClose}>
            Cerrar
          </Button>
        </div>
      </div>
    </Modal>
  )
}

function ResultadoCodigo({
  resultado,
  deEsteSorteo,
  onEntregar,
  onClose,
}: {
  resultado: PrizeDrawLookupResponse
  deEsteSorteo: boolean
  onEntregar: (prizeId: string) => void
  onClose: () => void
}) {
  return (
    <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 space-y-3">
      <div className="flex items-center justify-between border-b border-slate-200 dark:border-slate-700 pb-3">
        <div>
          <span className="text-[10px] uppercase font-bold text-blue-600 dark:text-blue-400 tracking-wider">
            Premio Encontrado
          </span>
          <h4 className="text-base font-black text-slate-900 dark:text-white">
            {resultado.prizeName}
          </h4>
        </div>
        {resultado.status === 'voided' ? (
          <span className="px-2.5 py-1 text-xs font-bold rounded-md bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300 flex items-center gap-1">
            <Ban className="w-3.5 h-3.5" /> {prizeDrawStatusLabel.voided}
          </span>
        ) : resultado.status === 'redeemed' ? (
          <span className="px-2.5 py-1 text-xs font-bold rounded-md bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 flex items-center gap-1">
            <CheckCircle2 className="w-3.5 h-3.5" /> {prizeDrawStatusLabel.redeemed}
          </span>
        ) : (
          <span className="px-2.5 py-1 text-xs font-bold rounded-md bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 flex items-center gap-1">
            <Clock className="w-3.5 h-3.5" /> {prizeDrawStatusLabel.drawn}
          </span>
        )}
      </div>

      <div className="grid grid-cols-2 gap-3 text-xs">
        <div>
          <span className="text-slate-500 dark:text-slate-400 block">Ganador(a):</span>
          <span className="font-bold text-slate-900 dark:text-white">{resultado.winnerName}</span>
        </div>
        <div>
          <span className="text-slate-500 dark:text-slate-400 block">Empresa:</span>
          <span className="font-medium text-slate-800 dark:text-slate-200">
            {resultado.winnerCompany ?? 'Particular'}
          </span>
        </div>
      </div>

      {resultado.void ? (
        <p className="text-xs text-rose-800 dark:text-rose-300">
          Este ganador se anuló el {formatDateTime(resultado.void.voidedAt)} (
          {prizeDrawVoidCodeLabel[resultado.void.code]}). El código ya no da derecho al premio.
        </p>
      ) : resultado.handover ? (
        <p className="text-xs text-emerald-800 dark:text-emerald-300">
          Entregado el {formatDateTime(resultado.handover.redeemedAt)} a{' '}
          <strong>{resultado.handover.receiverName}</strong>.
        </p>
      ) : deEsteSorteo ? (
        <Button
          type="button"
          onClick={() => onEntregar(resultado.prizeId)}
          className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer"
        >
          <CheckCircle2 className="w-4 h-4 mr-2" />
          Registrar entrega
        </Button>
      ) : (
        <Link
          to={`/sorteo/sorteos/${resultado.eventId}?entrega=${resultado.prizeId}`}
          // La misma página con otro sorteo: el escáner no se desmonta solo
          onClick={onClose}
          className="block text-center text-xs font-bold text-blue-600 hover:underline dark:text-blue-400"
        >
          Este código es de otro sorteo: abrir su entrega
        </Link>
      )}
    </div>
  )
}
