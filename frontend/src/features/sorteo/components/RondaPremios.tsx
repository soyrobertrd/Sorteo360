import { useId, useState } from 'react'
import { ChevronDown, Edit2, Plus, Trash2 } from 'lucide-react'
import type { FotoElegida } from '../hooks'
import type { PrizeResponse, RaffleRoundResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { Pagination } from '@/components/ui/Pagination'
import type { ViewMode } from '@/components/ui/ViewModeToggle'
import { agruparPremios } from '../raffleView'
import { PremioCard } from './PremioCard'
import { PremiosAgrupados } from './PremiosAgrupados'

/** Premios por página: diez filas en la lista, nueve tarjetas (tres filas de tres) en la cuadrícula. */
const POR_PAGINA: Record<ViewMode, number> = { list: 10, grid: 9 }

/**
 * Una ronda como acordeón: el encabezado la abre y la cierra, y sus premios se paginan para que
 * una ronda grande no empuje a las demás fuera de la pantalla.
 */
export function RondaPremios({
  ronda,
  vista,
  inicialmenteAbierta,
  canSetup,
  setupTitle,
  onEditar,
  onEliminar,
  onAgregarPremio,
  onEliminarPremio,
  onImagenPremio,
}: {
  ronda: RaffleRoundResponse
  vista: ViewMode
  inicialmenteAbierta: boolean
  canSetup: boolean
  setupTitle?: string
  onEditar: () => void
  onEliminar: () => void
  onAgregarPremio: () => void
  onEliminarPremio: (premio: PrizeResponse) => void
  /** Pone o quita la foto; quien la recibe la aplica a los premios iguales de la ronda. */
  onImagenPremio?: (premio: PrizeResponse, foto: FotoElegida) => void
}) {
  const [abierta, setAbierta] = useState(inicialmenteAbierta)
  const [pagina, setPagina] = useState(1)
  const cuerpoId = useId()

  const completados = ronda.prizes.filter((p) => p.state !== 'undrawn').length
  const conSorteos = ronda.prizes.some((p) => p.draws.length > 0)
  const porPagina = POR_PAGINA[vista]
  // 50 unidades del mismo premio son una línea con su cantidad, no cincuenta.
  const grupos = agruparPremios(ronda.prizes)
  const paginas = Math.max(1, Math.ceil(grupos.length / porPagina))
  // Si cambia la vista o se borran premios, la página no puede quedar fuera de rango.
  const paginaActual = Math.min(pagina, paginas)
  const visibles = grupos.slice((paginaActual - 1) * porPagina, paginaActual * porPagina)

  return (
    <section className="rounded-xl border border-slate-200 bg-card dark:border-slate-800">
      {/* Encabezado: el título abre y cierra; las acciones quedan aparte */}
      <div className="flex flex-col justify-between gap-2 p-4 sm:flex-row sm:items-center sm:px-5">
        <button
          type="button"
          onClick={() => setAbierta((v) => !v)}
          aria-expanded={abierta}
          aria-controls={cuerpoId}
          className="flex min-w-0 flex-1 cursor-pointer items-start gap-3 text-left"
        >
          <ChevronDown
            className={`mt-1 h-4 w-4 shrink-0 text-muted-foreground transition-transform ${
              abierta ? '' : '-rotate-90'
            }`}
          />
          <span className="min-w-0">
            <span className="flex flex-wrap items-center gap-2">
              <span className="rounded bg-brand px-2.5 py-0.5 text-xs font-bold text-white">
                Ronda {ronda.number}
              </span>
              <span className="text-base font-bold text-foreground">{ronda.name}</span>
            </span>
            {ronda.description && (
              <span className="mt-1 block text-xs text-muted-foreground">{ronda.description}</span>
            )}
          </span>
        </button>

        <div className="flex items-center gap-2">
          <span className="mr-2 text-xs font-semibold text-muted-foreground">
            {completados} de {ronda.prizes.length} sorteados
          </span>
          {canSetup && !conSorteos && (
            <button
              type="button"
              onClick={onEditar}
              className="cursor-pointer rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-muted hover:text-slate-700"
              title="Editar nombre de ronda"
              aria-label={`Editar ronda ${ronda.number}`}
            >
              <Edit2 className="h-3.5 w-3.5" />
            </button>
          )}
          {canSetup && ronda.prizes.length === 0 && (
            <button
              type="button"
              onClick={onEliminar}
              className="cursor-pointer rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-950"
              title="Eliminar ronda"
              aria-label={`Eliminar ronda ${ronda.number}`}
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={onAgregarPremio}
            disabled={!canSetup}
            title={setupTitle}
            className="ml-2 cursor-pointer text-xs font-semibold"
          >
            <Plus className="mr-1 h-3.5 w-3.5" />
            Premio en esta ronda
          </Button>
        </div>
      </div>

      {abierta && (
        <div
          id={cuerpoId}
          className="space-y-4 border-t border-slate-100 p-4 sm:p-5 dark:border-slate-800"
        >
          {ronda.prizes.length === 0 ? (
            <p className="py-6 text-center text-xs text-muted-foreground">
              No hay premios agregados a esta ronda aún.
            </p>
          ) : vista === 'list' ? (
            <PremiosAgrupados
              grupos={visibles}
              onDelete={canSetup ? onEliminarPremio : undefined}
              onImagen={onImagenPremio}
            />
          ) : (
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
              {visibles.map((g) => (
                <PremioCard
                  key={g.clave}
                  premio={g.premios[0]}
                  grupo={g.premios}
                  onDelete={
                    canSetup && g.premios.length === 1
                      ? () => onEliminarPremio(g.premios[0])
                      : undefined
                  }
                  onImagen={onImagenPremio ? (f) => onImagenPremio(g.premios[0], f) : undefined}
                />
              ))}
            </div>
          )}
          {paginas > 1 && (
            <Pagination
              page={paginaActual}
              pageCount={paginas}
              onPageChange={setPagina}
              total={grupos.length}
            />
          )}
        </div>
      )}
    </section>
  )
}
