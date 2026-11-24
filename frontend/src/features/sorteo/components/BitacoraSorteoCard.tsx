import { useState } from 'react'
import type { RaffleResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { Pagination } from '@/components/ui/Pagination'
import { descargarBlob } from '@/lib/download'
import { formatDateTime } from '@/lib/format'
import { accionLabel, bitacoraCsv, bitacoraDe } from '../bitacora'

const POR_PAGINA = 15

/** La bitácora de auditoría del sorteo, paginada y exportable a CSV. */
export function BitacoraSorteoCard({ raffle, nombre }: { raffle: RaffleResponse; nombre: string }) {
  const [pagina, setPagina] = useState(1)
  const entradas = bitacoraDe(raffle)
  const paginas = Math.max(1, Math.ceil(entradas.length / POR_PAGINA))
  const actual = Math.min(pagina, paginas)
  const visibles = entradas.slice((actual - 1) * POR_PAGINA, actual * POR_PAGINA)

  const exportar = () =>
    descargarBlob(
      new Blob(['\uFEFF' + bitacoraCsv(entradas)], { type: 'text/csv;charset=utf-8' }),
      `bitacora_${nombre.replace(/[^\p{L}\p{N}]+/gu, '_')}.csv`,
    )

  return (
    <section className="space-y-4 rounded-2xl border border-slate-200 bg-card p-5 dark:border-slate-800">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-base font-bold text-foreground">Bitácora de auditoría</h3>
          <p className="text-xs text-muted-foreground">
            Cada sorteo, anulación y entrega, con fecha, hora, ronda y quién lo hizo.
          </p>
        </div>
        <Button
          type="button"
          size="sm"
          variant="outline"
          onClick={exportar}
          disabled={entradas.length === 0}
          className="cursor-pointer text-xs font-bold"
        >
          Exportar CSV
        </Button>
      </div>

      {entradas.length === 0 ? (
        <p className="py-6 text-center text-xs text-muted-foreground">
          Todavía no hay movimientos.
        </p>
      ) : (
        <div className="table-scrollbar overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-800">
          <table className="w-full min-w-[720px] text-left text-xs">
            <thead className="bg-muted text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
              <tr>
                <th scope="col" className="px-3 py-2.5">
                  Fecha y hora
                </th>
                <th scope="col" className="px-3 py-2.5">
                  Acción
                </th>
                <th scope="col" className="px-3 py-2.5">
                  Ronda
                </th>
                <th scope="col" className="px-3 py-2.5">
                  Premio
                </th>
                <th scope="col" className="px-3 py-2.5">
                  Participante
                </th>
                <th scope="col" className="px-3 py-2.5">
                  Usuario
                </th>
                <th scope="col" className="px-3 py-2.5">
                  Detalle
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {visibles.map((e, i) => (
                <tr
                  key={`${e.fecha}-${e.accion}-${i}`}
                  className="odd:bg-card even:bg-brand/10 dark:even:bg-white/5"
                >
                  <td className="whitespace-nowrap px-3 py-2 text-foreground">
                    {formatDateTime(e.fecha)}
                  </td>
                  <td className="px-3 py-2 font-bold text-foreground">{accionLabel[e.accion]}</td>
                  <td className="px-3 py-2 text-muted-foreground">{e.ronda}</td>
                  <td className="px-3 py-2 text-foreground">{e.premio}</td>
                  <td className="px-3 py-2 text-foreground">{e.participante}</td>
                  <td className="px-3 py-2 text-muted-foreground">{e.usuario ?? '—'}</td>
                  <td className="px-3 py-2 text-muted-foreground">{e.detalle ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {paginas > 1 && (
        <Pagination
          page={actual}
          pageCount={paginas}
          onPageChange={setPagina}
          total={entradas.length}
        />
      )}
    </section>
  )
}
