import { Fragment, useState } from 'react'
import { ChevronRight, Gift } from 'lucide-react'
import type { FotoElegida } from '../hooks'
import type { PrizeResponse } from '@/api/types'
import { cn } from '@/lib/utils'
import { liveDraw, type GrupoRonda } from '../raffleView'
import { FotoPremioBoton } from './FotoPremioBoton'
import { PremiosTabla } from './PremioCard'
import { FotoPremio } from './FotoPremio'

const resumen = (premios: PrizeResponse[]) => ({
  sorteados: premios.filter((p) => p.state !== 'undrawn').length,
  entregados: premios.filter((p) => p.state === 'redeemed').length,
})

/**
 * Los premios de una ronda, una fila por premio con su cantidad: 50 bonos iguales son una línea
 * "× 50", no cincuenta. La fila se abre para ver cada unidad con su ganador y sus acciones.
 */
export function PremiosAgrupados({
  grupos,
  onDelete,
  onImagen,
}: {
  grupos: GrupoRonda[]
  onDelete?: (premio: PrizeResponse) => void
  onImagen?: (premio: PrizeResponse, foto: FotoElegida) => void
}) {
  const [abiertos, setAbiertos] = useState<ReadonlySet<string>>(new Set())
  const alternar = (clave: string) =>
    setAbiertos((prev) => {
      const next = new Set(prev)
      if (next.has(clave)) next.delete(clave)
      else next.add(clave)
      return next
    })

  return (
    <div className="table-scrollbar overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-800">
      <table className="w-full min-w-[640px] text-left text-xs">
        <thead className="bg-slate-50 text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:bg-slate-800/60 dark:text-slate-400">
          <tr>
            <th scope="col" className="w-8 px-2 py-2.5">
              <span className="sr-only">Abrir</span>
            </th>
            <th scope="col" className="px-3 py-2.5">
              Premio
            </th>
            <th scope="col" className="px-3 py-2.5">
              Valor
            </th>
            <th scope="col" className="px-3 py-2.5 text-right">
              Cantidad
            </th>
            <th scope="col" className="px-3 py-2.5">
              Ganador / avance
            </th>
            <th scope="col" className="px-3 py-2.5 text-right">
              <span className="sr-only">Acciones</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {grupos.map((g) => {
            const primero = g.premios[0]
            const cantidad = g.premios.length
            const { sorteados, entregados } = resumen(g.premios)
            const abierto = abiertos.has(g.clave)
            const unico = cantidad === 1
            const ganador = unico ? liveDraw(primero) : undefined
            return (
              <Fragment key={g.clave}>
                <tr className="align-middle hover:bg-slate-50/70 dark:hover:bg-slate-800/40">
                  <td className="px-2 py-3">
                    {!unico && (
                      <button
                        type="button"
                        onClick={() => alternar(g.clave)}
                        aria-expanded={abierto}
                        aria-label={`${abierto ? 'Cerrar' : 'Ver'} las ${cantidad} unidades de ${primero.name}`}
                        className="rounded-md p-1 text-slate-400 hover:bg-muted hover:text-foreground"
                      >
                        <ChevronRight
                          className={cn('h-4 w-4 transition-transform', abierto && 'rotate-90')}
                        />
                      </button>
                    )}
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex items-center gap-3">
                      {primero.imageUrl ? (
                        <FotoPremio
                          src={primero.imageUrl}
                          alt=""
                          loading="lazy"
                          className="h-10 w-10 shrink-0 rounded-lg bg-slate-50 object-contain p-0.5 dark:bg-slate-800/50"
                        />
                      ) : (
                        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-400 dark:bg-slate-800">
                          <Gift className="h-4 w-4" />
                        </span>
                      )}
                      <div className="min-w-0">
                        <p className="font-semibold text-slate-900 dark:text-white">
                          {primero.name}
                        </p>
                        {primero.description && (
                          <p className="truncate text-[11px] text-slate-500 dark:text-slate-400">
                            {primero.description}
                          </p>
                        )}
                        {primero.sponsor?.companyName && (
                          <p className="truncate text-[11px] text-slate-500 dark:text-slate-400">
                            Patrocina: {primero.sponsor.companyName}
                          </p>
                        )}
                      </div>
                    </div>
                  </td>
                  <td className="whitespace-nowrap px-3 py-3 font-semibold text-brand dark:text-brand-accent">
                    {primero.value || '—'}
                  </td>
                  <td className="px-3 py-3 text-right font-semibold tabular-nums text-slate-900 dark:text-white">
                    × {cantidad.toLocaleString('es-DO')}
                  </td>
                  <td className="px-3 py-3">
                    {unico ? (
                      ganador ? (
                        <>
                          <p className="font-semibold text-slate-900 dark:text-white">
                            {ganador.winnerName}
                          </p>
                          {ganador.winnerCompany && (
                            <p className="text-[11px] text-slate-500">{ganador.winnerCompany}</p>
                          )}
                        </>
                      ) : (
                        <span className="text-slate-400">Sin sortear</span>
                      )
                    ) : (
                      <div className="max-w-56">
                        <p className="text-slate-700 dark:text-slate-200">
                          <span className="font-semibold tabular-nums">{sorteados}</span> de{' '}
                          {cantidad} sorteados
                          {entregados > 0 && (
                            <span className="text-slate-500"> · {entregados} entregados</span>
                          )}
                        </p>
                        <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                          <div
                            className="h-full rounded-full bg-brand"
                            style={{ width: `${(sorteados / cantidad) * 100}%` }}
                          />
                        </div>
                      </div>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-3 py-3 text-right">
                    {onImagen && (
                      <FotoPremioBoton premio={primero} onImagen={(f) => onImagen(primero, f)} />
                    )}
                  </td>
                </tr>
                {abierto && (
                  <tr>
                    <td colSpan={6} className="bg-slate-50/60 px-3 py-3 dark:bg-slate-900/40">
                      {/* Las primeras 100 unidades; el resto se ve en Ganadores o buscando. */}
                      <PremiosTabla premios={g.premios.slice(0, 100)} onDelete={onDelete} />
                      {cantidad > 100 && (
                        <p className="mt-2 text-[11px] text-slate-500">
                          Se muestran 100 de {cantidad.toLocaleString('es-DO')} unidades.
                        </p>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
