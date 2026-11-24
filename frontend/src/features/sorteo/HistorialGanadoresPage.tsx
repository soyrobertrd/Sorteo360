import { useState, useMemo } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Trophy } from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { TextInput } from '@/components/ui/Field'
import { DataTable, type Column } from '@/components/ui/DataTable'
import type { RaffleListItem } from '@/api/types'
import { formatDate } from '@/lib/format'
import { useRaffleList } from './hooks'
import { EstadoSorteoBadge } from './components/EstadoSorteoBadge'

/**
 * Los sorteos con al menos un ganador vivo, del evento más nuevo al más viejo (Decisión 15). Los
 * ganadores de cada uno están en la pestaña Ganadores de su detalle. Solo lectura.
 */
/** Los ganadores de un sorteo están en la pestaña Ganadores de su detalle. */
const verGanadores = (s: RaffleListItem) => `/sorteo/sorteos/${s.eventId}?tab=ganadores`

export function HistorialGanadoresPage() {
  const navigate = useNavigate()
  const { data, isLoading, error } = useRaffleList()
  const [search, setSearch] = useState('')

  const sorteos = useMemo(() => data?.items ?? [], [data])
  const conGanadores = useMemo(
    () =>
      sorteos
        .filter((s) => s.drawnCount > 0)
        .sort((a, b) => b.eventStartDate.localeCompare(a.eventStartDate)),
    [sorteos],
  )
  const filtrados = useMemo(() => {
    const term = search.toLowerCase()
    return conGanadores.filter((s) => s.eventName.toLowerCase().includes(term))
  }, [conGanadores, search])

  const columns: Column<RaffleListItem>[] = [
    {
      key: 'evento',
      header: 'EVENTO',
      cell: (s) => (
        <span className="font-bold text-slate-900 dark:text-slate-100">{s.eventName}</span>
      ),
    },
    {
      key: 'fecha',
      header: 'FECHA DEL EVENTO',
      cell: (s) => <span className="text-slate-500">{formatDate(s.eventStartDate)}</span>,
    },
    { key: 'estado', header: 'ESTADO', cell: (s) => <EstadoSorteoBadge estado={s.state} /> },
    {
      key: 'ganadores',
      header: 'GANADORES',
      cell: (s) => (
        <span className="font-bold text-emerald-600 dark:text-emerald-400">
          {s.drawnCount}/{s.prizeCount}
        </span>
      ),
    },
    {
      key: 'entregados',
      header: 'ENTREGADOS',
      cell: (s) => (
        <span className="font-medium text-slate-600 dark:text-slate-400">{s.redeemedCount}</span>
      ),
    },
    {
      key: 'acciones',
      header: '',
      align: 'right',
      cell: (s) => (
        <Link to={verGanadores(s)} onClick={(e) => e.stopPropagation()}>
          <Button variant="outline" size="sm" className="text-xs font-semibold cursor-pointer">
            <Trophy className="w-3.5 h-3.5 mr-1" />
            Ver ganadores
          </Button>
        </Link>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      {/* Header + Breadcrumbs */}
      <div className="space-y-2">
        <Breadcrumbs
          items={[
            { label: 'Inicio', to: '/app' },
            { label: 'Rifas en vivo', to: '/sorteo' },
            { label: 'Historial de ganadores' },
          ]}
        />
        <div>
          <h1 className="text-2xl font-black tracking-tight text-slate-900 dark:text-slate-100">
            Historial de Ganadores
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Los sorteos que ya tienen ganadores, del evento más reciente al más antiguo.
          </p>
        </div>
      </div>

      {/* Búsqueda */}
      <Card className="p-4 bg-white dark:bg-card border border-slate-200 dark:border-slate-800">
        <div className="w-full md:w-80">
          <TextInput
            aria-label="Buscar por evento"
            placeholder="Buscar por evento…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </Card>

      {error != null && <ErrorNote error={error} />}
      {data && data.total > sorteos.length && (
        <p className="text-xs text-slate-500">
          Se revisan los {sorteos.length} sorteos de los eventos más recientes, de {data.total}.
        </p>
      )}

      {/* Tabla de Sorteos */}
      <Card className="overflow-hidden border border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-2xs">
        {isLoading ? (
          <Spinner />
        ) : (
          <DataTable<RaffleListItem>
            rows={filtrados}
            getRowKey={(s) => s.raffleId}
            onRowClick={(s) => navigate(verGanadores(s))}
            columns={columns}
            variant="default"
            className="rounded-none border-0 shadow-none"
            empty={
              <div className="py-8 text-center text-slate-500">
                <Trophy className="w-10 h-10 mx-auto mb-2 text-slate-300" />
                No hay sorteos con ganadores que coincidan con la búsqueda.
              </div>
            }
          />
        )}
      </Card>
    </div>
  )
}
