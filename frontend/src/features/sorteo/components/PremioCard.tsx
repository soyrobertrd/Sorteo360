import { Gift, CheckCircle2, Trophy, Trash2 } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import type { FotoElegida } from '../hooks'
import type { PrizeResponse } from '@/api/types'
import { prizeStateLabel } from '@/lib/labels'
import { liveDraw } from '../raffleView'
import { FotoPremioBoton } from './FotoPremioBoton'
import { FotoPremio } from './FotoPremio'

interface PremioCardProps {
  premio: PrizeResponse
  rondaNombre?: string
  /** Solo un premio que nunca se sorteó se puede eliminar, ni siquiera con un sorteo anulado. */
  onDelete?: () => void
  /** Si viene, se ofrece poner, cambiar o quitar la foto. */
  onImagen?: (foto: FotoElegida) => void
  /** Las unidades del mismo premio: con más de una, la tarjeta muestra la cantidad y el avance. */
  grupo?: PrizeResponse[]
}

export function PremioCard({ premio, rondaNombre, onDelete, onImagen, grupo }: PremioCardProps) {
  const cantidad = grupo?.length ?? 1
  const varios = cantidad > 1
  const sorteados = grupo ? grupo.filter((p) => p.state !== 'undrawn').length : 0
  const ganador = varios ? undefined : liveDraw(premio)
  return (
    <Card className="group relative overflow-hidden p-4 transition-all hover:border-slate-300 dark:hover:border-slate-700 bg-white dark:bg-card border border-slate-200 dark:border-slate-800">
      {/* Acciones */}
      <div className="absolute top-3 right-3 z-10 flex items-center gap-2 rounded-lg bg-white/85 dark:bg-card/85">
        {onImagen && <FotoPremioBoton premio={premio} onImagen={onImagen} />}
        {onDelete && premio.draws.length === 0 && (
          <button
            type="button"
            onClick={onDelete}
            className="text-slate-400 hover:text-rose-600 dark:hover:text-rose-400 p-1 transition-colors cursor-pointer"
            title="Eliminar premio"
          >
            <Trash2 size={14} />
          </button>
        )}
      </div>

      {/* La foto del premio, o el ícono si no tiene */}
      {premio.imageUrl ? (
        <FotoPremio
          src={premio.imageUrl}
          alt=""
          loading="lazy"
          className="-mx-4 -mt-4 mb-3 aspect-[4/3] w-[calc(100%+2rem)] max-w-none bg-slate-50 object-contain p-3 dark:bg-slate-800/50"
        />
      ) : (
        <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200 bg-slate-100 text-blue-600 dark:border-slate-700 dark:bg-slate-800 dark:text-blue-400">
          <Gift size={20} />
        </div>
      )}

      {/* Ronda badge si aplica */}
      {rondaNombre && (
        <span className="mb-1 inline-block text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
          {rondaNombre}
        </span>
      )}

      {/* Nombre y descripción */}
      <h3 className="font-sans text-sm font-bold text-slate-900 dark:text-white">{premio.name}</h3>
      {premio.description && (
        <p className="mt-1 font-body text-xs leading-relaxed text-slate-600 dark:text-slate-400">
          {premio.description}
        </p>
      )}

      {/* Valor */}
      {premio.value && (
        <p className="mt-2 font-sans text-sm font-bold text-blue-600 dark:text-blue-400">
          {premio.value}
        </p>
      )}

      {varios && (
        <p className="mt-2 text-xs text-slate-600 dark:text-slate-300">
          <span className="font-bold text-slate-900 tabular-nums dark:text-white">
            × {cantidad.toLocaleString('es-DO')}
          </span>{' '}
          · {sorteados} de {cantidad} sorteados
        </p>
      )}

      {/* Ganador */}
      {ganador && (
        <div className="mt-3 flex items-center gap-2 rounded-lg bg-slate-50 border border-slate-200 p-2 text-xs dark:bg-slate-800/60 dark:border-slate-700">
          <Trophy size={14} className="text-amber-600 dark:text-amber-400 shrink-0" />
          <div className="truncate">
            <span className="text-[10px] uppercase font-bold text-slate-400 block">Ganador</span>
            <span className="font-bold text-slate-900 dark:text-white truncate block">
              {ganador.winnerName}
            </span>
            {ganador.winnerCompany && (
              <span className="text-[10px] text-slate-500 dark:text-slate-400 truncate block">
                {ganador.winnerCompany}
              </span>
            )}
          </div>
        </div>
      )}

      {/* Estado de entrega: con varias unidades, el avance de arriba ya lo dice. */}
      {!varios && (
        <div className="mt-3 flex items-center justify-between border-t border-slate-100 pt-3 dark:border-slate-800">
          {premio.state === 'redeemed' ? (
            <span className="inline-flex items-center gap-1.5 text-[11px] font-bold text-emerald-700 dark:text-emerald-400">
              <CheckCircle2 size={14} />
              {prizeStateLabel.redeemed}
            </span>
          ) : (
            <span className="text-[11px] font-medium text-slate-400">
              {prizeStateLabel[premio.state]}
            </span>
          )}
        </div>
      )}
    </Card>
  )
}

/** Los premios de una ronda en filas: la vista Lista de la pestaña Rondas y Premios. */
export function PremiosTabla({
  premios,
  onDelete,
  onImagen,
}: {
  premios: PrizeResponse[]
  /** Si viene, cada premio sin sorteos ofrece eliminarse. */
  onDelete?: (premio: PrizeResponse) => void
  onImagen?: (premio: PrizeResponse, foto: FotoElegida) => void
}) {
  const acciones = Boolean(onDelete || onImagen)
  return (
    <div className="table-scrollbar overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-800">
      <table className="w-full min-w-[640px] text-left text-xs">
        <thead className="bg-slate-50 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:bg-slate-800/60 dark:text-slate-400">
          <tr>
            <th scope="col" className="px-4 py-2.5">
              Premio
            </th>
            <th scope="col" className="px-4 py-2.5">
              Valor
            </th>
            <th scope="col" className="px-4 py-2.5">
              Ganador
            </th>
            <th scope="col" className="px-4 py-2.5">
              Estado
            </th>
            {acciones && (
              <th scope="col" className="px-4 py-2.5 text-right">
                <span className="sr-only">Acciones</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {premios.map((premio) => {
            const ganador = liveDraw(premio)
            return (
              <tr
                key={premio.id}
                className="align-top odd:bg-card even:bg-brand/10 dark:even:bg-white/5"
              >
                <td className="px-4 py-3">
                  <div className="flex items-start gap-3">
                    {premio.imageUrl && (
                      <FotoPremio
                        src={premio.imageUrl}
                        alt=""
                        loading="lazy"
                        className="h-10 w-10 shrink-0 rounded-lg bg-slate-50 object-contain p-0.5 dark:bg-slate-800/50"
                      />
                    )}
                    <div>
                      <p className="font-bold text-slate-900 dark:text-white">{premio.name}</p>
                      {premio.description && (
                        <p className="mt-0.5 text-[11px] text-slate-500 dark:text-slate-400">
                          {premio.description}
                        </p>
                      )}
                    </div>
                  </div>
                </td>
                <td className="whitespace-nowrap px-4 py-3 font-bold text-brand dark:text-brand-accent">
                  {premio.value || '—'}
                </td>
                <td className="px-4 py-3">
                  {ganador ? (
                    <>
                      <p className="font-bold text-slate-900 dark:text-white">
                        {ganador.winnerName}
                      </p>
                      {ganador.winnerCompany && (
                        <p className="text-[11px] text-slate-500 dark:text-slate-400">
                          {ganador.winnerCompany}
                        </p>
                      )}
                    </>
                  ) : (
                    <span className="text-slate-400">—</span>
                  )}
                </td>
                <td className="whitespace-nowrap px-4 py-3">
                  <span
                    className={
                      premio.state === 'redeemed'
                        ? 'font-bold text-emerald-700 dark:text-emerald-400'
                        : 'text-slate-500 dark:text-slate-400'
                    }
                  >
                    {prizeStateLabel[premio.state]}
                  </span>
                </td>
                {acciones && (
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    {onImagen && (
                      <FotoPremioBoton premio={premio} onImagen={(f) => onImagen(premio, f)} />
                    )}
                    {onDelete && premio.draws.length === 0 && (
                      <button
                        type="button"
                        onClick={() => onDelete(premio)}
                        aria-label={`Eliminar premio ${premio.name}`}
                        className="cursor-pointer p-1 text-slate-400 transition-colors hover:text-rose-600 dark:hover:text-rose-400"
                      >
                        <Trash2 size={14} />
                      </button>
                    )}
                  </td>
                )}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
